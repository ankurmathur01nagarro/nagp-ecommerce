"""
promptly.py — A Rich-powered interactive prompt library.

Install dependencies:
    pip install rich readchar

All choice-based methods accept a structured array (list of dicts, objects, or plain strings).

  display_prop  — which property to show on screen  (default: str(item))
  value_prop    — which property to return           (default: the whole item)

Navigation:
  ↑ / k    move up
  ↓ / j    move down
  1–9      jump directly to item
  Enter    confirm selection
  Space    toggle (select_many only)

Usage:
    from promptly import ask, ask_int, ask_str, select_many, ranked, confirm, text_input
"""

from time import sleep

import readchar
from rich.console import Console
from rich.live import Live
from rich.panel import Panel
from rich.prompt import Prompt
from rich.table import Table
from rich.text import Text
from rich import box
from typing import Any

console = Console()

# ─────────────────────────────────────────────
# Types
# ─────────────────────────────────────────────

Item   = Any   # dict, object, dataclass, or plain string
Choice = Any   # resolved return value


# ─────────────────────────────────────────────
# Internal — prop resolution
# ─────────────────────────────────────────────

def _display(item: Item, display_prop: str | None) -> str:
    if display_prop is None:
        return str(item)
    if isinstance(item, dict):
        return str(item[display_prop])
    return str(getattr(item, display_prop))


def _value(item: Item, value_prop: str | None) -> Choice:
    if value_prop is None:
        return item
    if isinstance(item, dict):
        return item[value_prop]
    return getattr(item, value_prop)


# ─────────────────────────────────────────────
# Internal — rendering
# ─────────────────────────────────────────────

def _render_menu(
    items: list[Item],
    display_prop: str | None,
    focused: int,
    selected: set[int] | None = None,   # for multi-select
    border_style: str = "cyan",
    subtitle: str = "↑↓ navigate  Enter confirm",
) -> Table:
    """
    Render the choice list as a Rich Table.
    focused  — 0-based index of the highlighted row.
    selected — set of 0-based indices that are toggled on (multi-select).
    """
    # Single fused prefix column (caret + number) keeps everything tight
    table = Table(box=box.SIMPLE, show_header=False, padding=(0, 0), expand=False)
    table.add_column("prefix", no_wrap=True)
    table.add_column("choice", min_width=24, no_wrap=False)

    for i, item in enumerate(items):
        label       = _display(item, display_prop)
        is_focused  = (i == focused)
        is_selected = selected is not None and i in selected

        # "❯ [2] " when focused, "  [2] " otherwise — all one styled Text
        caret        = "❯ " if is_focused else "  "
        prefix_style = "bold cyan" if is_focused else "dim"
        prefix_cell  = Text(f"{caret}[{i+1}] ", style=prefix_style)

        label_style = "bold white on grey23" if is_focused else "white"

        # multi-select: "(●) label" selected, "( ) label" unselected
        if selected is not None:
            radio       = "(\u25cf) " if is_selected else "( ) "
            radio_style = "bold green" if is_selected else "dim"
            label_cell  = Text(radio, style=radio_style) + Text(label, style=label_style)
        else:
            label_cell = Text(label, style=label_style)

        table.add_row(prefix_cell, label_cell)

    return table


def _header_panel(prompt: str, border_style: str, subtitle: str) -> Panel:
    return Panel(
        Text(prompt, style="bold white"),
        border_style=border_style,
        expand=False,
        subtitle=f"[dim]{subtitle}[/]",
    )


# ─────────────────────────────────────────────
# Internal — arrow-key single select
# ─────────────────────────────────────────────

def _arrow_select(
    prompt: str,
    items: list[Item],
    display_prop: str | None,
    border_style: str = "cyan",
) -> int:
    """
    Full-screen arrow-key single-select.
    Returns 0-based index of the chosen item.
    """
    focused = 0
    subtitle = "↑ ↓  navigate    Enter  confirm    1-9  jump"

    console.print()
    console.print(_header_panel(prompt, border_style, subtitle))

    with Live(
        _render_menu(items, display_prop, focused, border_style=border_style),
        console=console,
        refresh_per_second=30,
        transient=False,
    ) as live:
        while True:
            key = readchar.readkey()

            if key in (readchar.key.UP, "k"):
                focused = (focused - 1) % len(items)

            elif key in (readchar.key.DOWN, "j"):
                focused = (focused + 1) % len(items)

            elif key.isdigit() and 1 <= int(key) <= len(items):
                focused = int(key) - 1

            elif key in (readchar.key.ENTER, "\r", "\n"):
                break

            live.update(_render_menu(items, display_prop, focused, border_style=border_style))

    return focused


# ─────────────────────────────────────────────
# Internal — arrow-key multi select
# ─────────────────────────────────────────────

def _arrow_multi_select(
    prompt: str,
    items: list[Item],
    display_prop: str | None,
) -> list[int]:
    """
    Arrow-key multi-select with Space to toggle.
    Returns list of selected 0-based indices.
    """
    focused  = 0
    selected: set[int] = set()
    subtitle = "↑ ↓  navigate    Space  toggle    Enter  confirm"

    console.print()
    console.print(_header_panel(prompt, "magenta", subtitle))

    with Live(
        _render_menu(items, display_prop, focused, selected, border_style="magenta"),
        console=console,
        refresh_per_second=30,
        transient=False,
    ) as live:
        while True:
            key = readchar.readkey()

            if key in (readchar.key.UP, "k"):
                focused = (focused - 1) % len(items)

            elif key in (readchar.key.DOWN, "j"):
                focused = (focused + 1) % len(items)

            elif key.isdigit() and 1 <= int(key) <= len(items):
                focused = int(key) - 1

            elif key == " ":
                if focused in selected:
                    selected.discard(focused)
                else:
                    selected.add(focused)

            elif key in (readchar.key.ENTER, "\r", "\n"):
                if selected:
                    break
                # nothing selected — flash a hint without breaking Live
                # (just move focus; user sees no change but can keep going)

            live.update(_render_menu(items, display_prop, focused, selected, border_style="magenta"))

    return sorted(selected)


# ─────────────────────────────────────────────
# Internal — arrow-key ranking
# ─────────────────────────────────────────────

def _arrow_rank(
    prompt: str,
    items: list[Item],
    display_prop: str | None,
) -> list[int]:
    """
    Arrow-key ranking: user builds an ordered list by pressing Enter on each
    item in their preferred order. Already-ranked items are shown with their rank number.
    Returns ranked list of 0-based indices.
    """
    focused = 0
    ranking: list[int] = []          # ordered list of chosen indices
    subtitle = "↑ ↓  navigate    Enter  pick next    (rank all items)"

    def _render_ranked() -> Table:
        table = Table(box=box.SIMPLE, show_header=False, padding=(0, 0), expand=False)
        table.add_column("prefix", no_wrap=True)
        table.add_column("choice", min_width=24)

        for i, item in enumerate(items):
            label      = _display(item, display_prop)
            is_focused = (i == focused)
            rank_pos   = ranking.index(i) + 1 if i in ranking else None

            if rank_pos is not None:
                # already ranked — show "#1 " prefix in green
                prefix_cell = Text(f"   #{rank_pos} ", style="bold green")
                label_cell  = Text(label, style="green")
            elif is_focused:
                prefix_cell = Text(f"❯ [{i+1}] ", style="bold cyan")
                label_cell  = Text(label, style="bold white on grey23")
            else:
                prefix_cell = Text(f"  [{i+1}] ", style="dim")
                label_cell  = Text(label, style="white")

            table.add_row(prefix_cell, label_cell)

        remaining = len(items) - len(ranking)
        table.add_section()
        table.add_row("", Text(f"{remaining} item(s) left to rank", style="dim italic"))
        return table

    console.print()
    console.print(_header_panel(prompt, "green", subtitle))

    with Live(_render_ranked(), console=console, refresh_per_second=30, transient=False) as live:
        while len(ranking) < len(items):
            key = readchar.readkey()

            if key in (readchar.key.UP, "k"):
                focused = (focused - 1) % len(items)

            elif key in (readchar.key.DOWN, "j"):
                focused = (focused + 1) % len(items)

            elif key.isdigit() and 1 <= int(key) <= len(items):
                focused = int(key) - 1

            elif key in (readchar.key.ENTER, "\r", "\n"):
                if focused not in ranking:
                    ranking.append(focused)
                    # auto-advance focus to next unranked item
                    for offset in range(1, len(items) + 1):
                        nxt = (focused + offset) % len(items)
                        if nxt not in ranking:
                            focused = nxt
                            break

            live.update(_render_ranked())

    return ranking


# ─────────────────────────────────────────────
# Public API — choice-based
# ─────────────────────────────────────────────

def ask(
    prompt: str,
    items: list[Item],
    *,
    display_prop: str | None = None,
    value_prop:   str | None = None,
) -> tuple[int, Choice]:
    """
    Single-select prompt with arrow-key navigation.

    Returns (1-based index, resolved value).

    Args:
        prompt       : Question shown to the user.
        items        : List of dicts, objects, dataclasses, or plain strings.
        display_prop : Property to display in the list     (default: str(item)).
        value_prop   : Property to return on selection     (default: whole item).

    Examples:
        # Plain strings
        idx, val = ask("Pick a color", ["Red", "Green", "Blue"])

        # Dicts — display one prop, return whole dict
        idx, lang = ask("Pick", langs, display_prop="name")

        # Dicts — display one prop, return another
        idx, lang_id = ask("Pick", langs, display_prop="name", value_prop="id")
    """
    idx      = _arrow_select(prompt, items, display_prop, border_style="cyan")
    item     = items[idx]
    label    = _display(item, display_prop)
    resolved = _value(item, value_prop)
    console.print(f"  [green]✔[/] [bold]{idx + 1}. {label}[/]\n")
    return idx + 1, resolved


def ask_int(
    prompt: str,
    items: list[Item],
    *,
    display_prop: str | None = None,
) -> int:
    """Like ask(), but returns only the 1-based integer index."""
    index, _ = ask(prompt, items, display_prop=display_prop)
    return index


def ask_str(
    prompt: str,
    items: list[Item],
    *,
    display_prop: str | None = None,
    value_prop:   str | None = None,
) -> Choice:
    """Like ask(), but returns only the resolved value (no index)."""
    _, value = ask(prompt, items, display_prop=display_prop, value_prop=value_prop)
    return value


def select_many(
    prompt: str,
    items: list[Item],
    *,
    display_prop: str | None = None,
    value_prop:   str | None = None,
) -> list[Choice]:
    """
    Multi-select prompt with arrow-key navigation and Space to toggle.

    Returns a list of resolved values for each selected item (in selection order).

    Example:
        features = [
            {"label": "Speed",     "id": "spd"},
            {"label": "Safety",    "id": "saf"},
            {"label": "Ergonomics","id": "erg"},
        ]
        chosen = select_many("Pick features", features,
                             display_prop="label", value_prop="id")
        # → ["spd", "erg"]
    """
    indices        = _arrow_multi_select(prompt, items, display_prop)
    selected_labels = ", ".join(_display(items[i], display_prop) for i in indices)
    resolved        = [_value(items[i], value_prop) for i in indices]
    console.print(f"  [green]✔[/] Selected: [bold]{selected_labels}[/]\n")
    return resolved


def ranked(
    prompt: str,
    items: list[Item],
    *,
    display_prop: str | None = None,
    value_prop:   str | None = None,
) -> list[Choice]:
    """
    Rank-order prompt with arrow-key navigation.

    User presses Enter on each item in their preferred order.
    Already-ranked items are shown with their rank number (#1, #2…).
    Returns resolved values in ranked order.

    Example:
        priorities = [{"name": "Speed"}, {"name": "Quality"}, {"name": "Cost"}]
        order = ranked("Rank these", priorities,
                       display_prop="name", value_prop="name")
        # → ["Quality", "Cost", "Speed"]
    """
    indices  = _arrow_rank(prompt, items, display_prop)
    labels   = [_display(items[i], display_prop) for i in indices]
    resolved = [_value(items[i], value_prop) for i in indices]
    console.print(f"  [green]✔[/] Ranking: {' → '.join(labels)}\n")
    return resolved


# ─────────────────────────────────────────────
# Public API — non-choice inputs (unchanged)
# ─────────────────────────────────────────────

def confirm(prompt: str, default: bool = True) -> bool:
    """
    Yes/No confirmation prompt.

    Example:
        if confirm("Deploy to production?"):
            deploy()
    """
    console.print()
    hint = "[Y/n]" if default else "[y/N]"
    console.print(Panel(
        Text(f"{prompt}  {hint}", style="bold white"),
        border_style="yellow",
        expand=False,
        subtitle="[dim]Confirmation[/]",
    ))
    while True:
        raw = Prompt.ask("[bold yellow]Your answer[/]", default="y" if default else "n")
        raw = raw.strip().lower()
        if raw in ("y", "yes"):
            console.print("  [green]✔[/] Confirmed.\n")
            return True
        elif raw in ("n", "no"):
            console.print("  [red]✗[/] Cancelled.\n")
            return False
        console.print("  [red]✗[/] Please enter y or n.")


def text_input(prompt: str, placeholder: str = "") -> str:
    """
    Free-form text input.

    Example:
        name = text_input("What's your name?", placeholder="e.g. Alice")
    """
    console.print()
    subtitle = f"[dim]{placeholder}[/]" if placeholder else "[dim]Free text[/]"
    console.print(Panel(
        Text(prompt, style="bold white"),
        border_style="blue",
        expand=False,
        subtitle=subtitle,
    ))
    while True:
        raw = Prompt.ask("[bold yellow]Your answer[/]")
        if raw.strip():
            console.print(f"  [green]✔[/] Got it: [bold]{raw.strip()}[/]\n")
            return raw.strip()
        console.print("  [red]✗[/] Input cannot be empty.")


# ─────────────────────────────────────────────
# Demo
# ─────────────────────────────────────────────

if __name__ == "__main__":
    console.rule("[bold cyan]promptly demo[/]")

    name = text_input("What's your name?", placeholder="e.g. Alice")

    # ── 1. Plain strings ─────────────────────────────────────────────────
    idx, color = ask("Favourite color?", ["Red", "Green", "Blue"])

    # ── 2. Dicts — display_prop only → returns whole dict ────────────────
    langs = [
        {"name": "Python",     "paradigm": "multi"},
        {"name": "Rust",       "paradigm": "systems"},
        {"name": "TypeScript", "paradigm": "web"},
        {"name": "Go",         "paradigm": "concurrent"},
        {"name": "Elixir",     "paradigm": "functional"},
    ]
    _, lang = ask("Preferred language?", langs, display_prop="name")

    # ── 3. Dicts — value_prop → returns just that field ──────────────────
    features = [
        {"label": "Performance", "id": "perf"},
        {"label": "Readability", "id": "read"},
        {"label": "Ecosystem",   "id": "eco"},
        {"label": "Type Safety", "id": "types"},
        {"label": "Concurrency", "id": "conc"},
    ]
    chosen_ids = select_many(
        "Which features matter?", features,
        display_prop="label", value_prop="id"
    )

    # ── 4. Objects with ranking ───────────────────────────────────────────
    class Priority:
        def __init__(self, name, weight):
            self.name   = name
            self.weight = weight
        def __repr__(self):
            return f"Priority({self.name!r}, w={self.weight})"

    priorities = [Priority("Speed", 3), Priority("Quality", 5), Priority("Cost", 2)]
    ordered = ranked(
        "Rank these priorities", priorities,
        display_prop="name",
        value_prop=None        # returns whole Priority objects
    )

    proceed = confirm(f"All done, {name}. Proceed?")

    # ── Summary ──────────────────────────────────────────────────────────
    console.rule("[bold green]Summary[/]")
    console.print(f"  Name       : [bold]{name}[/]")
    console.print(f"  Color      : [bold]{color}[/]  (index {idx})")
    console.print(f"  Language   : [bold]{lang['name']}[/]  → full dict: {lang}")
    console.print(f"  Feature IDs: [bold]{', '.join(chosen_ids)}[/]")
    console.print(f"  Priorities : [bold]{' > '.join(p.name for p in ordered)}[/]")
    console.print(f"  Proceed    : [bold]{'Yes' if proceed else 'No'}[/]")
    console.print()

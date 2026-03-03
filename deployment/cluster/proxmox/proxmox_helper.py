from ipaddress import ip_address
import socket
from time import sleep
import time
import paramiko
from fabric import Connection
from proxmoxer import ProxmoxAPI
from proxmoxer.tools import Path, Tasks
from rich import print
from rich.prompt import IntPrompt, Prompt
from rich.progress import Progress, SpinnerColumn, TextColumn
from rich.console import Console
from rich.text import Text
import promptly as p

c = Console()

def get_ssh_publickey():
    """Get SSH public keys from ~/.ssh folder (either rsa or ed25519) to inject into VM"""
    ssh_keys = ""
    ssh_key_files = [f for f in Path.home().joinpath(".ssh").iterdir() if f.is_file() and f.suffix in ['.pub']]
    if ssh_key_files:
        ssh_keys = "\n".join([f.read_text() for f in ssh_key_files])
        c.log(f"[green]Found SSH public keys in ~/.ssh[/]")
    else:
        c.log(f"[yellow]No SSH public keys found in ~/.ssh[/]")

    return ssh_keys, ssh_key_files

def await_task(prox, task):
    c.rule("[bold blue]Proxmox Task Log[/]")
    logs = []
    while True:
        sleep(1)  # avoid hammering the API; adjust as needed
        task_status = prox.nodes('pve').tasks(task).status.get()
        if task_status is not None and task_status['status'] == 'stopped':
            if task_status['exitstatus'] != 'OK':
                c.log(f"[red]Task failed with exit status {task_status['exitstatus']}[/]")
            else:
                c.log(f"[green]Task completed successfully.[/]")
            break
        else:
            lines = max(logs, key=lambda l: l['n']) if logs else {'n': -1}
            logs = prox.nodes('pve').tasks(task).log.get()
            logs = list(logs) if logs is not None else []
            # Only print new log lines since last check
            new_logs = [l for l in logs if l['n'] > lines['n']]
            for log in new_logs:
                log_content = log['t']
                c.print(log_content, style="grey50 dim")
                
def ask_storage(prox: ProxmoxAPI, content_type: str = 'images'):
    # Get cluster storage that support importing disk images
    storage_result = prox.cluster.resources.get(type='storage')
    storages = list(storage_result) if storage_result is not None else []
    storages = [s for s in storages if s['content'] and content_type in s['content']]
    
    storage_choices = [{
        "display": f"{s['storage']} (contents: {s['content']})",
        "value": s
    } for s in storages]
    _, selected_storage = p.ask("Press Enter to select storage", storage_choices, display_prop="display", value_prop="value")
    vm_storage = selected_storage['storage']
    return vm_storage

def start_vm(prox, vm_id, ip_address):
    c.rule(f"[bold blue]Starting VM {vm_id}[/]")
    task = prox.nodes('pve').qemu(vm_id).status.start.post()
    await_task(prox, task)
    # Wait until VM is fully started
    while True:
        vm_status = prox.nodes('pve').qemu(vm_id).status.current.get()
        if vm_status is not None and vm_status['status'] == 'running':
            break
        else:
            c.log(f"[blue]Waiting for VM {vm_id} to start...[/]")
            sleep(2)
            
    wait_for_ssh(ip_address)
    with Connection(f"k3s@{ip_address}") as conn:
        c.log(f"[green]Waiting for cloud-init to finish...[/green]")
        result = conn.run("cloud-init status --wait", pty=True, warn=True)  # optional: wait for cloud-init to fully finish
        # Then check yourself what actually happened
        if result.exited == 2:
            c.log("[yellow]⚠ cloud-init finished with warnings, continuing...[/yellow]")
        elif result.exited != 0:
            raise RuntimeError(f"cloud-init failed with exit code {result.exited}")
        else:
            c.log("[green]✅ cloud-init finished successfully.[/green]")
    c.log(f"[blue]Started VM {vm_id}[/]")

def wait_for_ssh(ip: str, port: int = 22, timeout: int = 120, interval: int = 3):
    """Wait until SSH port is open — means cloud-init is done enough to accept connections."""
    c.log(f"[yellow]Waiting for SSH on {ip}:{port}...[/yellow]")
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            with socket.create_connection((ip, port), timeout=5):
                c.log(f"[green]SSH is up on {ip}![/green]")
                return
        except (socket.timeout, ConnectionRefusedError, OSError):
            time.sleep(interval)
    raise TimeoutError(f"SSH on {ip} not reachable after {timeout}s.")

def run_script_over_ssh(ip: str, script: str, title: str = "SSH Command Output"):
    with Connection(f"k3s@{ip}") as conn:
        c.rule(f"[bold blue]{title}[/]")
        result = conn.run(script, hide=True)
        c.print(Text(result.stdout.strip(), style="grey50 dim"))

        # Interactive shell
        # conn.run("bash", pty=True)
        c.rule(f"[bold green]✅ Done[/]")
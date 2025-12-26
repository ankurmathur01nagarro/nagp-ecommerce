import { Component, ElementRef, type AfterViewInit, forwardRef, input, viewChild, contentChildren, contentChild, signal, computed, afterRenderEffect } from '@angular/core';
import { NgTemplateOutlet } from "@angular/common";

@Component({
  selector: 'App-NavBar',
  templateUrl: './nav-bar-component.html',
  styleUrl: './nav-bar-component.css',
  imports: [forwardRef(() => NavMenu), forwardRef(() => NavItem), forwardRef(() => SubMenu), forwardRef(() => CustomSubMenu), forwardRef(() => SubMenuColumn)],
})
export class NavBarComponent {

}

@Component({
  selector: 'div[App-NavMenu]',
  host: {
    '[class.top-menu]': 'true',
  },
  template: `
    <!-- main menu begin -->
    <div class="container">
      <nav #mainMenu class="main-menu">
        <ul class="nav-menu">
          <ng-content />
        </ul>
      </nav>
    </div>
    <!-- main menu end -->`,
})
export class NavMenu implements AfterViewInit {
  mainMenu = viewChild.required('mainMenu', { read: ElementRef<HTMLElement> });
  subMenuItems = contentChildren(NavItem);

  constructor() {
    afterRenderEffect(() => {
      if (this.subMenuItems().length > 0) {
        // Logic to handle submenu items
        this.subMenuItems().forEach(item => {
          item.isNavItem.set(true);
        });
      }
    });
  }

  ngAfterViewInit(): void {
    jQuery(() => {
      this.initializeMegaMenu();
    });
  }

  private initializeMegaMenu(): void {
    jQuery(this.mainMenu().nativeElement).accessibleMegaMenu({
      /* prefix for generated unique id attributes, which are required
         to indicate aria-owns, aria-controls and aria-labelledby */
      uuidPrefix: "accessible-megamenu",
      /* css class used to define the megamenu styling */
      menuClass: "nav-menu",
      /* css class for a top-level navigation item in the megamenu */
      topNavItemClass: "nav-item",
      /* css class for a megamenu panel */
      panelClass: "sub-nav",
      /* css class for a group of items within a megamenu panel */
      panelGroupClass: "sub-nav-group",
      /* css class for the hover state */
      hoverClass: "hover",
      /* css class for the focus state */
      focusClass: "focus",
      /* css class for the open state */
      openClass: "open"
    });
  }
}

@Component({
  selector: 'li[App-NavItem]',
  host: {
    '[class.nav-item]': 'isNavItem()',
    '[class.has-sub-nav]': 'isSubMenu() && hasSubItems()',
  },
  template: `
    <ng-container>
      <a [href]="link() || '#'">
        @if (icon()) {
          <span><i [class]="icon()">&nbsp;</i>{{label()}}</span>
        } @else {
          <span>{{label()}}</span>
        }
      </a>
      <ng-content />
    </ng-container>
  `,
})
export class NavItem {
  label = input.required<string>();
  link = input.required<string>();
  icon = input<string>();

  isSubMenu = signal(false);
  isNavItem = signal(false);
  childSubMenu = contentChild(SubMenu, { descendants: false });
  hasSubItems = computed(() => {
    const subMenu = this.childSubMenu();
    return !!subMenu;
  });
}

@Component({
  selector: 'div[App-SubMenu]',
  host: {
    '[class.sub-nav]': 'true',
  },
  template: `
    <ul class="sub-nav-group">
      <ng-content />
    </ul>
  `,
})
export class SubMenu {
  subMenuItems = contentChildren(NavItem);

  constructor() {
    afterRenderEffect(() => {
      if (this.subMenuItems().length > 0) {
        // Logic to handle submenu items
        this.subMenuItems().forEach(item => {
          item.isSubMenu.set(true);
        });
      }
    });
  }
}

@Component({
  selector: 'div[App-CustomSubMenu]',
  host: {
    '[class]': "'sub-nav full padding'",
  },
  template: `
    <ng-template #content>
      <ng-content></ng-content>
    </ng-template>
    @if (type() === 'tabular') {
      <div class="row">
        <ng-container *ngTemplateOutlet="content"></ng-container>
      </div>
    } @else {
      <ng-container *ngTemplateOutlet="content"></ng-container>
    }
  `,
  imports: [NgTemplateOutlet],
})
export class CustomSubMenu {
  type = input.required<"tabular" | "custom">();
}

@Component({
  selector: 'div[App-SubMenuColumn]',
  host: {
    '[class]': 'columnSize()',
  },
  template: `
    <ng-container>
      <h3 class="sub-nav-title">{{header()}}</h3>
      <ul class="sub-nav-group sub-nav-grey">
        <ng-content></ng-content>
      </ul>
    </ng-container>
  `,
})
export class SubMenuColumn {
  header = input.required<string>();
  size = input.required<number>();

  columnSize = computed(() => `col-xs-${this.size()}`);

  items = contentChildren(NavItem);
}

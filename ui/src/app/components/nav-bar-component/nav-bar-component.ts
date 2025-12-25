import { Component, ElementRef, ViewChild, type AfterViewInit, type OnInit, type TemplateRef } from '@angular/core';
import { NgTemplateOutlet } from "@angular/common";

type NavBarItemData = {
  label: string;
  link?: string;
  icon?: string;
}

type SimpleNavBarItem = NavBarItemData & {
  subItemType: 'simple';
  subItems?: NavBarItemData[];
};

type TabularNavBarItem = NavBarItemData & {
  subItemType: 'tabular';
  columns: {
    header: string;
    size: number;
    items: NavBarItemData[];
  }[];
}

type MegaNavBarItem = NavBarItemData & {
  subItemType: 'mega';
  template: TemplateRef<any>;
}

type NavBarItem = SimpleNavBarItem | TabularNavBarItem | MegaNavBarItem;

@Component({
  selector: 'App-NavBar',
  templateUrl: './nav-bar-component.html',
  styleUrl: './nav-bar-component.css',
  imports: [NgTemplateOutlet],
})
export class NavBarComponent implements OnInit, AfterViewInit {
  @ViewChild('mainMenu') mainMenu!: ElementRef<HTMLElement>;
  @ViewChild('megaTemplate', { static: true }) megaTemplate!: TemplateRef<any>;

  navBarItemCollection: NavBarItem[] = [];

  ngOnInit(): void {
    this.navBarItemCollection = [
      { label: 'Home', subItemType: 'simple', icon: 'icon-screen-desktop', link: '/' },
      { label: 'Categories', subItemType: 'simple', icon: 'icon-bag', subItems: [
        { label: 'Electronics', icon:'icon-screen-smartphone', link: '/categories/electronics' },
        { label: 'Books', link: '/categories/books' },
        { label: 'Clothing', link: '/categories/clothing' },
      ]},
      { label: 'Deals', subItemType: 'tabular', link: '/deals', icon: 'icon-tag', columns: [
        {
          header: 'Today\'s Deals',
          size: 6,
          items: [
            { label: 'Electronics Deals', link: '/deals/electronics' },
            { label: 'Fashion Deals', link: '/deals/fashion' },
          ]
        },
        {
          header: 'Upcoming Deals',
          size: 6,
          items: [
            { label: 'Holiday Deals', link: '/deals/holiday' },
            { label: 'Clearance Sales', link: '/deals/clearance' },
          ]
        }],
      },
      { label: 'Mega Menu', subItemType: 'mega', icon: 'icon-grid', template: this.megaTemplate },
      { label: 'Contact Us', subItemType: 'simple', icon: 'icon-call-in', link: '/contact' },
    ];
  }

  ngAfterViewInit(): void {
    jQuery(() => {
      this.initializeMegaMenu();
    });
  }

  private initializeMegaMenu(): void {
    jQuery(this.mainMenu.nativeElement).accessibleMegaMenu({
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

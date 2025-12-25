// Type definitions for jQuery accessibleMegaMenu plugin
// Project: https://github.com/adobe-accessibility/Accessible-Mega-Menu
// Definitions by: [Your Name or Copilot]

/// <reference types="jquery" />

interface AccessibleMegaMenuOptions {
    /**
     * Prefix for generated unique id attributes, which are required to indicate aria-owns, aria-controls and aria-labelledby
     * @default "accessible-megamenu"
     */
    uuidPrefix?: string;
    /**
     * CSS class used to define the megamenu styling
     * @default "accessible-megamenu"
     */
    menuClass?: string;
    /**
     * CSS class for a top-level navigation item in the megamenu
     * @default "accessible-megamenu-top-nav-item"
     */
    topNavItemClass?: string;
    /**
     * CSS class for a megamenu panel
     * @default "accessible-megamenu-panel"
     */
    panelClass?: string;
    /**
     * CSS class for a group of items within a megamenu panel
     * @default "accessible-megamenu-panel-group"
     */
    panelGroupClass?: string;
    /**
     * CSS class for the hover state
     * @default "hover"
     */
    hoverClass?: string;
    /**
     * CSS class for the focus state
     * @default "focus"
     */
    focusClass?: string;
    /**
     * CSS class for the open state
     * @default "open"
     */
    openClass?: string;
}

interface JQuery {
    /**
     * Initializes the accessibleMegaMenu plugin on the selected elements.
     * @param options Options for configuring the mega menu.
     */
    accessibleMegaMenu(options?: AccessibleMegaMenuOptions): JQuery;

    /**
     * Calls a method on the accessibleMegaMenu plugin instance.
     * @param method The method name to call.
     * @param args Additional arguments for the method.
     */
    accessibleMegaMenu(method: 'getDefaults'): object;
    accessibleMegaMenu(method: 'getOption', opt: string): string;
    accessibleMegaMenu(method: 'getAllOptions'): object;
    accessibleMegaMenu(method: 'setOption', opt: string, val: string, reinitialize?: boolean): void;
}
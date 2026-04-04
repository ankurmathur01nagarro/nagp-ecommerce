import { Component, input } from '@angular/core';
import type { IconName } from '@app/models/icon-name';

@Component({
  selector: 'li[appDropdownMenu]',
  templateUrl: './dropdown-menu-button.component.html',
  host: {
    class: 'btn-group dropdown',
  },
})
export class DropdownMenuButtonComponent {
  readonly icon = input.required<IconName>();
  readonly label = input<string>();
  readonly heading = input<string>();
  readonly hideLabelOn = input<string>('hidden-sm hidden-xs');
  readonly menuClass = input<string>();
}

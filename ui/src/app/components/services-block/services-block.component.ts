import { Component, input } from '@angular/core';
import type { IconName } from '@app/models/icon-name';

export type ServiceItem = {
  icon: IconName;
  title: string;
  description: string;
  link?: string;
};

@Component({
  selector: 'App-ServicesBlock',
  templateUrl: './services-block.component.html',
})
export class ServicesBlockComponent {
  readonly items = input.required<ServiceItem[]>();
}

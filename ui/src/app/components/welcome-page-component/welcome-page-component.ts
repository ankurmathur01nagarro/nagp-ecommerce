import { Component } from '@angular/core';
import { ServicesBlockComponent, type ServiceItem } from '@app/components/services-block/services-block.component';

@Component({
  selector: 'App-WelcomePage',
  imports: [ServicesBlockComponent],
  templateUrl: './welcome-page-component.html',
  styleUrl: './welcome-page-component.css',
})
export class WelcomePageComponent {
  readonly services: ServiceItem[] = [
    {
      icon: 'icon-like',
      title: 'Free Shipping',
      description: 'Lorem ipsum dolor sit amet, adipisicing elit, sed do eiusmod tempor enim ad minim nostrud exercitation ullamco consequat irure dolor in reprehenderit omnis voluptatem accusantium.',
    },
    {
      icon: 'icon_currency',
      title: 'Back Guarantee',
      description: 'Lorem ipsum dolor sit amet, adipisicing elit, sed do eiusmod tempor enim ad minim nostrud exercitation ullamco consequat irure dolor in reprehenderit omnis voluptatem accusantium.',
    },
    {
      icon: 'icon-speedometer',
      title: 'Fastest Dilivery',
      description: 'Lorem ipsum dolor sit amet, adipisicing elit, sed do eiusmod tempor enim ad minim nostrud exercitation ullamco consequat irure dolor in reprehenderit omnis voluptatem accusantium.',
    },
  ];
}

import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from "../Header/header.component";
import { NavBarComponent } from "../nav-bar-component/nav-bar-component";

@Component({
  selector: 'App-Home',
  imports: [RouterOutlet, HeaderComponent, NavBarComponent],
  templateUrl: './home-component.html',
  styleUrl: './home-component.css',
})
export class HomeComponent {

}

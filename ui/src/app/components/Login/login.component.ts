import { Component, inject } from "@angular/core";
import { WebApiService } from "../../services/web-api-service";

@Component({
  selector: "App-Login",
  templateUrl: "./login.component.html",
  styleUrl: "./login.component.css",
})
export class LoginComponent {
  readonly webApiService = inject(WebApiService);


}

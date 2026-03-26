import { Component, inject, signal } from "@angular/core";
import { WebApiService } from "../../services/web-api-service";
import { form, FormField, required } from "@angular/forms/signals";

interface LoginData {
  username: string;
  password: string;
}

@Component({
  selector: "App-Login",
  templateUrl: "./login.component.html",
  styleUrl: "./login.component.css",
  imports: [FormField]
})
export class LoginComponent {
  readonly webApiService = inject(WebApiService);

  readonly loginModel = signal<LoginData>({
    username: "",
    password: "",
  });

  readonly loginForm = form(this.loginModel, p => {
    required(p.username);
    required(p.password);
  });

  readonly token = this.webApiService.token;

  onSubmit() {
    this.webApiService.login(this.loginModel().username, this.loginModel().password);
  }
}

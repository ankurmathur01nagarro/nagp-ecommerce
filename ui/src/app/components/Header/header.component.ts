import { Component, effect, inject, signal } from "@angular/core";
import { AuthService, type UserInfo } from "@app/services/auth-service";
import { AuthStore } from "@app/store/auth.store";
import { lastValueFrom } from "rxjs";
import { DropdownMenuButtonComponent } from "@app/components/dropdown-menu-button/dropdown-menu-button.component";

@Component({
  selector: "App-Header",
  templateUrl: "./header.component.html",
  imports: [DropdownMenuButtonComponent],
})
export class HeaderComponent {
  readonly store = inject(AuthStore);
  readonly authService = inject(AuthService);

  constructor() {
    effect(async () => {
      if (this.store.isLoggedIn()) {
        const userInfo = await lastValueFrom(this.authService.userInfo());
        console.log("User info:", userInfo);
        this.user.set(userInfo);
      }
    });
  }

  readonly user = signal<UserInfo | null>(null);
}

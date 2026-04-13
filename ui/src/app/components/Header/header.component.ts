import { Component, effect, inject, signal } from "@angular/core";
import { Router } from "@angular/router";
import { AuthService, type UserInfo } from "@app/services/auth-service";
import { AuthStore } from "@app/store/auth.store";
import { lastValueFrom } from "rxjs";
import { DropdownMenuButtonComponent } from "@app/components/dropdown-menu-button/dropdown-menu-button.component";
import { CartStore } from "@app/store/cart.store";

@Component({
  selector: "App-Header",
  templateUrl: "./header.component.html",
  imports: [DropdownMenuButtonComponent],
})
export class HeaderComponent {
  readonly store = inject(AuthStore);
  readonly authService = inject(AuthService);
  readonly cartStore = inject(CartStore);
  private readonly router = inject(Router);

  constructor() {
    effect(async () => {
      const userInfo = await lastValueFrom(this.authService.userInfo());
      console.log("User info:", userInfo);
      this.user.set(userInfo);
    });
  }

  readonly user = signal<UserInfo | null>(null);

  logout(): void {
    this.store.logout();
    this.router.navigate(['/notloggedin']);
  }
}

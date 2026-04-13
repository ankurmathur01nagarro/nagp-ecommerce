import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthStore, type TokenResponse } from '@app/store/auth.store';

@Component({
  selector: 'app-authorization-handler',
  imports: [],
  templateUrl: './authorization-handler.html',
  styleUrl: './authorization-handler.css',
})
export class AuthorizationHandlerComponent {
  constructor() {
    const route = inject(ActivatedRoute);
    const router = inject(Router);
    const authStore = inject(AuthStore);

    const params = route.snapshot.queryParamMap;
    const token = params.get('token');
    const username = params.get('username');
    const expiresAt = params.get('expiresAt');
    const returnPath = params.get('returnPath') ?? '/';

    if (token && username && expiresAt) {
      const user: TokenResponse = { token, username, expiresAt: expiresAt };
      authStore.setUserFromExternal(user);
      localStorage.setItem('loginSession', JSON.stringify(user));
    }

    router.navigateByUrl(returnPath);
  }
}

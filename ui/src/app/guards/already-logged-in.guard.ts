import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '@app/store/auth.store';

export const alreadyLoggedInGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.user() && !authStore.isExpired()) {
    return router.parseUrl('/');
  }

  return true;
};

import { inject } from '@angular/core';
import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { AuthStore } from '@app/store/auth.store';

export const SKIP_AUTH = new HttpContextToken<boolean>(() => false);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.context.get(SKIP_AUTH)) return next(req);

  const authStore = inject(AuthStore);

  if (authStore.isExpired()) {
    authStore.logout();
    return next(req);
  }

  const token = authStore.user()?.token;
  if (!token) return next(req);

  return next(req.clone({
    headers: req.headers.set('Authorization', `Bearer ${token}`)
  }));
};

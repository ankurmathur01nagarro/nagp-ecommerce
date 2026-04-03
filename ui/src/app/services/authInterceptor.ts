import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import type { TokenResponse } from './auth-service';

export const SKIP_AUTH = new HttpContextToken<boolean>(() => false);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.context.get(SKIP_AUTH)) return next(req);

  const sessionInfo = localStorage.getItem('loginSession');
  if (!sessionInfo) return next(req);

  const { token } = JSON.parse(sessionInfo) as TokenResponse;
  if (!token) return next(req);

  return next(req.clone({
    headers: req.headers.set('Authorization', `Bearer ${token}`)
  }));
};

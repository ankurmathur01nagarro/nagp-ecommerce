import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SKIP_AUTH } from './authInterceptor';
import { AuthStore } from '@app/store/auth.store';

export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.context.get(SKIP_AUTH)) return next(req);

  const router = inject(Router);
  const authStore = inject(AuthStore);

  return next(req).pipe(
    catchError(error => {
      if (error.status === 401) {
        authStore.logout();
        router.navigate(['/notloggedin']);
      }
      return throwError(() => error);
    })
  );
};

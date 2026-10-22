import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { Auth } from './auth';

// Adds the token to every request; a 401 on an authenticated request means it
// expired or is invalid, so log out.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const token = auth.token();
  const authed = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(authed).pipe(tap({ error: (e) => e.status === 401 && token && auth.logout() }));
};

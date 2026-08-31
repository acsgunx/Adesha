import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Endpoints that mint or rotate credentials: they must never carry (or retry with) a bearer token. */
const CREDENTIAL_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/setup', '/api/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  if (CREDENTIAL_ENDPOINTS.some((endpoint) => req.url.startsWith(endpoint))) {
    return next(req);
  }

  return from(auth.validAccessToken()).pipe(
    switchMap((token) => next(withBearer(req, token))),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !auth.hasRefreshToken()) {
        return throwError(() => error);
      }
      // The access token was rejected mid-flight: rotate once and replay the request.
      return from(auth.refresh()).pipe(
        switchMap((token) => (token ? next(withBearer(req, token)) : throwError(() => error)))
      );
    })
  );
};

function withBearer(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ headers: req.headers.set('Authorization', `Bearer ${token}`) }) : req;
}

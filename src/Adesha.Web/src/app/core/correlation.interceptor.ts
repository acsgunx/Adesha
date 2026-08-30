import { HttpInterceptorFn } from '@angular/common/http';

let correlationCounter = 0;

export const correlationInterceptor: HttpInterceptorFn = (req, next) => {
  const id = `${Date.now().toString(36)}-${(++correlationCounter).toString(36)}`;
  return next(req.clone({ headers: req.headers.set('X-Correlation-Id', id) }));
};

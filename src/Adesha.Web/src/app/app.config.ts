import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { AuthService } from './core/auth.service';
import { correlationInterceptor } from './core/correlation.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([correlationInterceptor, authInterceptor])),
    // Rule 2: the trading-mode banner must be accurate on the very first paint,
    // including a reload straight into the dashboard.
    provideAppInitializer(() => inject(AuthService).loadStatus().catch(() => undefined)),
  ],
};

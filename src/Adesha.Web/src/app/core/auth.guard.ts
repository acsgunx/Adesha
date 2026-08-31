import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  // A restored session may hold an expired access token; rotate before admitting the route.
  if (await auth.validAccessToken()) {
    return true;
  }
  return router.parseUrl('/login');
};

/** Owner setup is reachable only until an owner exists. */
export const setupGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!(await loadSetupState(auth))) {
    return true;
  }
  return auth.setupRequired() ? true : router.parseUrl('/login');
};

/**
 * On a fresh install there is no account to log in with, so the login page sends the
 * operator to owner setup instead of failing every submission with "invalid credentials".
 */
export const loginGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (await auth.validAccessToken()) {
    return router.parseUrl('/dashboard');
  }
  if (!(await loadSetupState(auth))) {
    return true;
  }
  return auth.setupRequired() ? router.parseUrl('/setup') : true;
};

/** Returns false when the API is unreachable, so guards fall back to rendering the route. */
async function loadSetupState(auth: AuthService): Promise<boolean> {
  try {
    await auth.loadStatus();
    return true;
  } catch {
    return false;
  }
}

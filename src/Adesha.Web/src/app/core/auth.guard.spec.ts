import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { authGuard, loginGuard, setupGuard } from './auth.guard';

describe('auth guards', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { parseUrl: (url: string) => ({ toString: () => url }) as UrlTree } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  function run(guard: CanActivateFn): Promise<boolean | UrlTree> {
    return runInInjectionContext(TestBed.inject(EnvironmentInjector), () =>
      guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
    ) as Promise<boolean | UrlTree>;
  }

  /** Lets the guard's promise chain settle so its HTTP calls are actually issued. */
  const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

  async function flushSetupRequired(setupRequired: boolean): Promise<void> {
    await tick();
    http.expectOne('/api/system/status').flush({ tradingMode: 'Disabled', environment: 'Development' });
    http.expectOne('/api/system/setup-required').flush({ setupRequired });
  }

  it('sends a first-run visitor from login to owner setup', async () => {
    const result = run(loginGuard);
    await flushSetupRequired(true);
    expect(String(await result)).toBe('/setup');
  });

  it('keeps the visitor on login once an owner exists', async () => {
    const result = run(loginGuard);
    await flushSetupRequired(false);
    expect(await result).toBe(true);
  });

  it('renders login when the API is unreachable', async () => {
    const result = run(loginGuard);
    await tick();
    http.expectOne('/api/system/status').error(new ProgressEvent('error'));
    http.expectOne('/api/system/setup-required').error(new ProgressEvent('error'));
    expect(await result).toBe(true);
  });

  it('closes owner setup once an owner is enrolled', async () => {
    const result = run(setupGuard);
    await flushSetupRequired(false);
    expect(String(await result)).toBe('/login');
  });

  it('redirects to login when there is no session', async () => {
    expect(String(await run(authGuard))).toBe('/login');
    expect(router.parseUrl('/login')).toBeTruthy();
  });

  it('admits a stored session after refreshing an expired access token', async () => {
    localStorage.setItem(
      'adesha.session',
      JSON.stringify({
        accessToken: 'stale',
        accessTokenExpiresAtUtc: new Date(Date.now() - 1000).toISOString(),
        refreshToken: 'refresh-1',
      })
    );

    const result = run(authGuard);
    await tick();
    http.expectOne('/api/auth/refresh').flush({
      accessToken: 'fresh',
      accessTokenExpiresAtUtc: new Date(Date.now() + 600_000).toISOString(),
      refreshToken: 'refresh-2',
    });

    expect(await result).toBe(true);
  });

  afterEach(() => {
    http.verify();
  });
});

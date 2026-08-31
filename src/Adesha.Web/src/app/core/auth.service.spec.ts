import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService, TokenPair } from './auth.service';

const STORAGE_KEY = 'adesha.session';

/** Lets the pending promise chain settle so the next HTTP call is actually issued. */
const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

function tokenPair(overrides: Partial<TokenPair> = {}): TokenPair {
  return {
    accessToken: 'access-1',
    accessTokenExpiresAtUtc: new Date(Date.now() + 600_000).toISOString(),
    refreshToken: 'refresh-1',
    ...overrides,
  };
}

describe('AuthService', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.fn>;

  function createService(): AuthService {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    localStorage.clear();
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.resetTestingModule();
  });

  afterEach(() => {
    http.verify();
  });

  it('persists the session so a page reload stays logged in', async () => {
    const auth = createService();

    const login = auth.login('owner', 'password', '123456');
    http.expectOne('/api/auth/login').flush(tokenPair());
    await tick();
    http.expectOne('/api/system/status').flush({ tradingMode: 'Paper', environment: 'Development' });
    http.expectOne('/api/system/setup-required').flush({ setupRequired: false });
    await login;

    expect(auth.isAuthenticated()).toBe(true);
    expect(navigate).toHaveBeenCalledWith(['/dashboard']);
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).refreshToken).toBe('refresh-1');

    TestBed.resetTestingModule();
    expect(createService().isAuthenticated()).toBe(true);
  });

  it('rotates an expired access token instead of dropping the session', async () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(tokenPair({ accessTokenExpiresAtUtc: new Date(Date.now() - 1000).toISOString() }))
    );
    const auth = createService();

    const token = auth.validAccessToken();
    const refresh = http.expectOne('/api/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush(tokenPair({ accessToken: 'access-2', refreshToken: 'refresh-2' }));

    expect(await token).toBe('access-2');
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).refreshToken).toBe('refresh-2');
  });

  it('shares one refresh request between concurrent callers', async () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(tokenPair()));
    const auth = createService();

    const both = Promise.all([auth.refresh(), auth.refresh()]);
    http.expectOne('/api/auth/refresh').flush(tokenPair({ accessToken: 'access-2' }));

    expect(await both).toEqual(['access-2', 'access-2']);
  });

  it('clears the stored session when the refresh token is rejected', async () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(tokenPair()));
    const auth = createService();

    const token = auth.refresh();
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await token).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('revokes the refresh token on logout and returns to the login page', async () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(tokenPair()));
    const auth = createService();

    const logout = auth.logout();
    const revoke = http.expectOne('/api/auth/logout');
    expect(revoke.request.body).toEqual({ refreshToken: 'refresh-1' });
    revoke.flush('');
    await logout;

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});

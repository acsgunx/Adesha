import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

export interface TokenPair {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
}

export interface SystemStatus {
  tradingMode: 'Disabled' | 'Paper' | 'Live';
  environment: string;
}

/** Refresh this long before expiry so an in-flight request never races the clock. */
const REFRESH_SKEW_MS = 30_000;
const SESSION_STORAGE_KEY = 'adesha.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _session = signal<TokenPair | null>(readStoredSession());
  private readonly _status = signal<SystemStatus | null>(null);
  private readonly _setupRequired = signal<boolean>(false);
  private refreshInFlight: Promise<string | null> | null = null;

  readonly isAuthenticated = computed(() => this._session() !== null);
  readonly tradingMode = computed(() => this._status()?.tradingMode ?? 'Disabled');
  readonly setupRequired = this._setupRequired.asReadonly();

  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  async loadStatus(): Promise<void> {
    const [status, setup] = await Promise.all([
      firstValueFrom(this.http.get<SystemStatus>('/api/system/status')),
      firstValueFrom(this.http.get<{ setupRequired: boolean }>('/api/system/setup-required')),
    ]);
    this._status.set(status);
    this._setupRequired.set(setup.setupRequired);
  }

  async setup(username: string, password: string): Promise<{ sharedKey: string; otpauthUri: string }> {
    return firstValueFrom(
      this.http.post<{ sharedKey: string; otpauthUri: string }>('/api/auth/setup', { username, password })
    );
  }

  async confirmTotp(username: string, password: string, totpCode: string): Promise<void> {
    await firstValueFrom(
      this.http.post('/api/auth/setup/confirm-totp', { username, password, totpCode }, { responseType: 'text' })
    );
    this._setupRequired.set(false);
  }

  async login(username: string, password: string, totpCode: string): Promise<void> {
    const tokens = await firstValueFrom<TokenPair>(
      this.http.post<TokenPair>('/api/auth/login', { username, password, totpCode })
    );
    this.storeSession(tokens);
    await this.loadStatus();
    await this.router.navigate(['/dashboard']);
  }

  async logout(): Promise<void> {
    const refreshToken = this._session()?.refreshToken;
    this.clearSession();
    if (refreshToken) {
      try {
        await firstValueFrom(this.http.post('/api/auth/logout', { refreshToken }, { responseType: 'text' }));
      } catch {
        // The local session is already gone; a failed revoke must not trap the user.
      }
    }
    await this.router.navigate(['/login']);
  }

  /**
   * Exchanges the stored refresh token for a new pair. Concurrent callers share a single
   * request: the backend rotates refresh tokens and treats a replayed one as a compromise.
   */
  refresh(): Promise<string | null> {
    this.refreshInFlight ??= this.rotateRefreshToken().finally(() => {
      this.refreshInFlight = null;
    });
    return this.refreshInFlight;
  }

  /** Returns a usable access token, refreshing first when the current one is about to expire. */
  async validAccessToken(): Promise<string | null> {
    const session = this._session();
    if (session === null) {
      return null;
    }
    return this.isExpiring(session) ? await this.refresh() : session.accessToken;
  }

  token(): string | null {
    return this._session()?.accessToken ?? null;
  }

  hasRefreshToken(): boolean {
    return this._session() !== null;
  }

  status(): SystemStatus | null {
    return this._status();
  }

  private async rotateRefreshToken(): Promise<string | null> {
    const refreshToken = this._session()?.refreshToken;
    if (!refreshToken) {
      return null;
    }
    try {
      const tokens = await firstValueFrom<TokenPair>(
        this.http.post<TokenPair>('/api/auth/refresh', { refreshToken })
      );
      this.storeSession(tokens);
      return tokens.accessToken;
    } catch {
      this.clearSession();
      return null;
    }
  }

  private isExpiring(session: TokenPair): boolean {
    const expiresAt = Date.parse(session.accessTokenExpiresAtUtc);
    return Number.isNaN(expiresAt) || expiresAt - Date.now() <= REFRESH_SKEW_MS;
  }

  private storeSession(tokens: TokenPair): void {
    this._session.set(tokens);
    try {
      localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(tokens));
    } catch {
      // Private browsing or a full quota: the in-memory session still works.
    }
  }

  private clearSession(): void {
    this._session.set(null);
    try {
      localStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // Nothing to clean up if storage is unavailable.
    }
  }
}

function readStoredSession(): TokenPair | null {
  try {
    const raw = localStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as Partial<TokenPair>;
    if (typeof parsed.accessToken !== 'string' || typeof parsed.refreshToken !== 'string') {
      return null;
    }
    return {
      accessToken: parsed.accessToken,
      accessTokenExpiresAtUtc: parsed.accessTokenExpiresAtUtc ?? new Date(0).toISOString(),
      refreshToken: parsed.refreshToken,
    };
  } catch {
    return null;
  }
}

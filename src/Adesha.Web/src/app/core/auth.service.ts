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

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _token = signal<string | null>(null);
  private readonly _status = signal<SystemStatus | null>(null);
  private readonly _setupRequired = signal<boolean>(true);

  readonly isAuthenticated = computed(() => this._token() !== null);
  readonly tradingMode = computed(() => this._status()?.tradingMode ?? 'Disabled');
  readonly setupRequired = this._setupRequired.asReadonly;

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
    this._token.set(tokens.accessToken);
    await this.loadStatus();
    await this.router.navigate(['/dashboard']);
  }

  async logout(): Promise<void> {
    this._token.set(null);
    await this.router.navigate(['/login']);
  }

  token(): string | null {
    return this._token();
  }

  status(): SystemStatus | null {
    return this._status();
  }
}

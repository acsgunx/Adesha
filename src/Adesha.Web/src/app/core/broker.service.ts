import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface BrokerCapabilities {
  brokerId: string;
  displayName: string;
  supportsOtpLogin: boolean;
  supportsTotpLogin: boolean;
  supportsInstrumentMaster: boolean;
  supportsLtpQuotes: boolean;
  supportsOhlcQuotes: boolean;
  supportsOrderBook: boolean;
  supportsTradeBook: boolean;
  supportsPositions: boolean;
  supportsHoldings: boolean;
  supportsFunds: boolean;
  supportsOrderPlacement: boolean;
  supportsOrderModification: boolean;
  supportsOrderCancellation: boolean;
  supportedExchanges: string[];
  supportedProducts: string[];
  supportedOrderTypes: string[];
}

export interface BrokerSession {
  isLoggedIn: boolean;
  brokerId: string;
  userId?: string;
  expiresAtUtc?: string;
  exchanges: string[];
  products: string[];
  orderTypes: string[];
}

@Injectable({ providedIn: 'root' })
export class BrokerService {
  private readonly http = inject(HttpClient);

  async getCapabilities(): Promise<BrokerCapabilities[]> {
    return firstValueFrom(this.http.get<BrokerCapabilities[]>('/api/broker/capabilities'));
  }

  async getSession(brokerId: string): Promise<BrokerSession> {
    return firstValueFrom(this.http.get<BrokerSession>(`/api/broker/session?brokerId=${encodeURIComponent(brokerId)}`));
  }

  async initiateLogin(brokerId: string, username: string, password: string): Promise<void> {
    await firstValueFrom(
      this.http.post('/api/broker/login/initiate', { brokerId, username, password }, { responseType: 'text' })
    );
  }

  async completeOtpLogin(brokerId: string, otp: string): Promise<BrokerSession> {
    return firstValueFrom(
      this.http.post<BrokerSession>('/api/broker/login/complete-otp', { brokerId, otp })
    );
  }

  async completeTotpLogin(brokerId: string, totp: string): Promise<BrokerSession> {
    return firstValueFrom(
      this.http.post<BrokerSession>('/api/broker/login/complete-totp', { brokerId, totp })
    );
  }

  async logout(brokerId: string): Promise<void> {
    await firstValueFrom(
      this.http.post('/api/broker/logout', { brokerId }, { responseType: 'text' })
    );
  }
}

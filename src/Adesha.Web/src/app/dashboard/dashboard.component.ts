import { Component, inject } from '@angular/core';
import { AuthService } from '../core/auth.service';
import { TradingModeBannerComponent } from '../trading-mode-banner/trading-mode-banner.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [TradingModeBannerComponent],
  template: `
    <app-trading-mode-banner />
    <div class="container">
      <h1>Adesha Dashboard</h1>
      <p>Trading mode: <strong>{{ auth.tradingMode() }}</strong></p>
      <p>Order management and market data come in later work orders.</p>
      <button (click)="auth.logout()">Log out</button>
    </div>
  `,
  styles: [
    `
      .container {
        max-width: 48rem;
        margin: 2rem auto;
        padding: 1.5rem;
      }
    `,
  ],
})
export class DashboardComponent {
  readonly auth = inject(AuthService);
}

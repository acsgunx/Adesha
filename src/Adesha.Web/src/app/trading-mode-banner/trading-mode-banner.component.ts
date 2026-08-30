import { Component, computed, inject } from '@angular/core';
import { AuthService } from '../core/auth.service';
import { TradingModeService } from '../core/trading-mode.service';

@Component({
  selector: 'app-trading-mode-banner',
  standalone: true,
  template: `
    <div class="banner" [style.background-color]="style().background" [style.color]="style().color">
      <strong>Trading mode: {{ tradingMode() }}</strong>
      @if (tradingMode() === 'Disabled') {
        <span> — no orders will reach a broker until this is set to Paper or Live.</span>
      }
    </div>
  `,
  styles: [
    `
      .banner {
        padding: 0.75rem 1rem;
        text-align: center;
        font-size: 0.95rem;
      }
    `,
  ],
})
export class TradingModeBannerComponent {
  private readonly auth = inject(AuthService);
  private readonly styleService = inject(TradingModeService);

  readonly tradingMode = this.auth.tradingMode;
  readonly style = computed(() => this.styleService.colors(this.tradingMode()));
}

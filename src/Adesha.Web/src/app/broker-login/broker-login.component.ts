import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { BrokerCapabilities, BrokerService, BrokerSession } from '../core/broker.service';
import { TradingModeBannerComponent } from '../trading-mode-banner/trading-mode-banner.component';

type BrokerLoginStep = 'credentials' | 'otp' | 'totp' | 'done';

@Component({
  selector: 'app-broker-login',
  standalone: true,
  imports: [ReactiveFormsModule, TradingModeBannerComponent, DatePipe],
  template: `
    <app-trading-mode-banner />
    <div class="container">
      <h1>Broker login — {{ brokerName() }}</h1>

      @switch (step()) {
        @case ('credentials') {
          <form [formGroup]="credentialsForm" (ngSubmit)="onInitiate()">
            <label for="username">m.Stock username / client code</label>
            <input id="username" formControlName="username" type="text" />
            <label for="password">Password</label>
            <input id="password" formControlName="password" type="password" />
            <button type="submit" [disabled]="credentialsForm.invalid || loading()">Send OTP / initiate</button>
          </form>
        }
        @case ('otp') {
          <p>Enter the OTP sent to your registered mobile number.</p>
          <form [formGroup]="otpForm" (ngSubmit)="onCompleteOtp()">
            <label for="otp">OTP</label>
            <input id="otp" formControlName="otp" type="text" inputmode="numeric" />
            <button type="submit" [disabled]="otpForm.invalid || loading()">Complete login</button>
          </form>
          @if (supportsTotp()) {
            <button type="button" class="secondary" (click)="switchToTotp()">Use TOTP instead</button>
          }
        }
        @case ('totp') {
          <p>Enter the current 6-digit TOTP from your authenticator app.</p>
          <form [formGroup]="totpForm" (ngSubmit)="onCompleteTotp()">
            <label for="totp">TOTP code</label>
            <input id="totp" formControlName="totp" type="text" inputmode="numeric" pattern="[0-9]*" />
            <button type="submit" [disabled]="totpForm.invalid || loading()">Complete login</button>
          </form>
          <button type="button" class="secondary" (click)="step.set('otp')">Use OTP instead</button>
        }
        @case ('done') {
          <p>Logged in as <strong>{{ session()?.userId }}</strong>.</p>
          <p>Session expires at {{ session()?.expiresAtUtc | date:'medium' }}.</p>
          <button type="button" (click)="onLogout()">Log out of broker</button>
          <button type="button" class="secondary" (click)="router.navigate(['/dashboard'])">Back to dashboard</button>
        }
      }

      @if (error()) {
        <p class="error">{{ error() }}</p>
      }
    </div>
  `,
  styles: [
    `
      .container {
        max-width: 28rem;
        margin: 2rem auto;
        padding: 1.5rem;
      }
      form {
        display: grid;
        gap: 0.5rem;
      }
      input,
      button {
        padding: 0.5rem;
        font-size: 1rem;
      }
      button + button,
      button.secondary {
        margin-top: 0.5rem;
        background: transparent;
      }
      .error {
        color: #a00;
      }
    `,
  ],
})
export class BrokerLoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly broker = inject(BrokerService);
  readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly step = signal<BrokerLoginStep>('credentials');
  readonly capabilities = signal<BrokerCapabilities | null>(null);
  readonly session = signal<BrokerSession | null>(null);

  readonly brokerName = computed(() => this.capabilities()?.displayName ?? 'm.Stock');
  readonly supportsTotp = computed(() => this.capabilities()?.supportsTotpLogin ?? false);

  readonly credentialsForm = this.fb.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  readonly otpForm = this.fb.group({
    otp: ['', [Validators.required]],
  });

  readonly totpForm = this.fb.group({
    totp: ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
  });

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const caps = await this.broker.getCapabilities();
      const mstock = caps.find((c) => c.brokerId === 'MStock');
      if (!mstock) {
        this.error.set('No broker is configured.');
        return;
      }
      this.capabilities.set(mstock);
      const session = await this.broker.getSession('MStock');
      if (session.isLoggedIn) {
        this.session.set(session);
        this.step.set('done');
      }
    } catch (err: unknown) {
      this.error.set(describeBrokerError(err));
    } finally {
      this.loading.set(false);
    }
  }

  async onInitiate(): Promise<void> {
    if (this.credentialsForm.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    const v = this.credentialsForm.value;
    try {
      await this.broker.initiateLogin('MStock', v.username!, v.password!);
      this.step.set('otp');
    } catch (err: unknown) {
      this.error.set(describeBrokerError(err));
    } finally {
      this.loading.set(false);
    }
  }

  async onCompleteOtp(): Promise<void> {
    if (this.otpForm.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const session = await this.broker.completeOtpLogin('MStock', this.otpForm.value.otp!);
      this.finish(session);
    } catch (err: unknown) {
      this.error.set(describeBrokerError(err));
    } finally {
      this.loading.set(false);
    }
  }

  async onCompleteTotp(): Promise<void> {
    if (this.totpForm.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const session = await this.broker.completeTotpLogin('MStock', this.totpForm.value.totp!);
      this.finish(session);
    } catch (err: unknown) {
      this.error.set(describeBrokerError(err));
    } finally {
      this.loading.set(false);
    }
  }

  switchToTotp(): void {
    this.step.set('totp');
  }

  async onLogout(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.broker.logout('MStock');
      this.step.set('credentials');
      this.session.set(null);
    } catch (err: unknown) {
      this.error.set(describeBrokerError(err));
    } finally {
      this.loading.set(false);
    }
  }

  private finish(session: BrokerSession): void {
    this.session.set(session);
    this.step.set('done');
  }
}

function describeBrokerError(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return 'Cannot reach the Adesha API.';
    }
    const body = err.error;
    if (typeof body === 'string' && body.length > 0) {
      return body;
    }
    if (body && typeof body === 'object') {
      const message = (body as { error?: string; title?: string }).error
        ?? (body as { title?: string }).title;
      if (message) return message;
    }
  }
  return 'Broker request failed. Please try again.';
}

import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { TradingModeBannerComponent } from '../trading-mode-banner/trading-mode-banner.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, TradingModeBannerComponent],
  template: `
    <app-trading-mode-banner />
    <div class="container">
      <h1>Adesha</h1>
      <form [formGroup]="form" (ngSubmit)="onSubmit()">
        <label for="username">Username</label>
        <input id="username" formControlName="username" type="text" />
        <label for="password">Password</label>
        <input id="password" formControlName="password" type="password" />
        <label for="totpCode">TOTP code</label>
        <input id="totpCode" formControlName="totpCode" type="text" inputmode="numeric" pattern="[0-9]*" />
        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
        <button type="submit" [disabled]="form.invalid || loading()">Log in</button>
      </form>
    </div>
  `,
  styles: [
    `
      .container {
        max-width: 24rem;
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
      .error {
        color: #a00;
      }
    `,
  ],
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(12)]],
    totpCode: ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
  });

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const v = this.form.value;
    try {
      await this.auth.login(v.username!, v.password!, v.totpCode!);
    } catch {
      this.error.set('Login failed. Check your password and TOTP code.');
      this.loading.set(false);
    }
  }
}

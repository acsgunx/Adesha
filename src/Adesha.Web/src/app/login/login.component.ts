import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { UNREACHABLE_STATUSES } from '../core/api-errors';
import { AuthService } from '../core/auth.service';
import { TradingModeBannerComponent } from '../trading-mode-banner/trading-mode-banner.component';

type LoginMethod = 'password' | 'passwordTotp';

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

        <fieldset class="method">
          <legend>Login method</legend>
          <label class="radio">
            <input type="radio" formControlName="method" value="password" />
            Password only
          </label>
          <label class="radio">
            <input type="radio" formControlName="method" value="passwordTotp" />
            Password + TOTP
          </label>
        </fieldset>

        @if (totpVisible()) {
          <label for="totpCode">TOTP code</label>
          <input id="totpCode" formControlName="totpCode" type="text" inputmode="numeric" pattern="[0-9]*" />
        }
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
      .method {
        border: 1px solid #ccc;
        padding: 0.5rem;
      }
      .radio {
        display: block;
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

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]],
    method: ['password' as LoginMethod, [Validators.required]],
    totpCode: ['', [Validators.pattern(/^[0-9]{6}$/)]],
  });

  /** TOTP field is shown only when the user picks the Password + TOTP method. */
  readonly totpVisible = computed(() => this.form.controls.method.value === 'passwordTotp');

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const v = this.form.value;
    // Send the TOTP code only when the TOTP method is selected; otherwise the backend
    // treats the request as a password-only login.
    const totpCode = v.method === 'passwordTotp' ? v.totpCode ?? '' : undefined;
    try {
      await this.auth.login(v.username!, v.password!, totpCode);
    } catch (error: unknown) {
      this.error.set(describeLoginFailure(error, v.method === 'passwordTotp'));
      this.loading.set(false);
    }
  }
}

function describeLoginFailure(error: unknown, usedTotp: boolean): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Login failed. Please try again.';
  }
  if (UNREACHABLE_STATUSES.has(error.status)) {
    return 'Cannot reach the Adesha API. Check that the backend is running.';
  }
  switch (error.status) {
    case 400:
      return 'Enter a username, a password, and (if using TOTP) a 6-digit code.';
    case 401:
      // A 401 can mean bad credentials, or that the chosen method does not match the
      // account's TOTP setting. Point the user at the method selector so they can switch.
      return usedTotp
        ? 'Invalid username or password, or TOTP is not enabled for this account. Try "Password only".'
        : 'Invalid username or password, or this account requires a TOTP code. Try "Password + TOTP".';
    case 423:
      return 'Account locked after too many failed attempts. Try again in 15 minutes.';
    default:
      return 'Login failed because the server returned an error. Check the API logs.';
  }
}

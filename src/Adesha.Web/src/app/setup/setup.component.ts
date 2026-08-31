import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DomSanitizer } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { TradingModeBannerComponent } from '../trading-mode-banner/trading-mode-banner.component';

@Component({
  selector: 'app-setup',
  standalone: true,
  imports: [ReactiveFormsModule, TradingModeBannerComponent],
  template: `
    <app-trading-mode-banner />
    <div class="container">
      <h1>Adesha — Owner setup</h1>
      @if (!sharedKey()) {
        <form [formGroup]="form" (ngSubmit)="onCreate()">
          <label for="username">Owner username</label>
          <input id="username" formControlName="username" type="text" />
          <label for="password">Password</label>
          <input id="password" formControlName="password" type="password" />
          <button type="submit" [disabled]="form.invalid || loading()">Create owner</button>
          @if (error()) {
            <p class="error">{{ error() }}</p>
          }
        </form>
      } @else {
        <div class="totp">
          <p>Scan this in your authenticator app, then enter the current code.</p>
          <code class="secret">{{ sharedKey() }}</code>
          <a [href]="otpauthLink()" target="_blank" rel="noopener">Open in authenticator</a>
          <form [formGroup]="confirmForm" (ngSubmit)="onConfirm()">
            <label for="totpCode">Current TOTP code</label>
            <input id="totpCode" formControlName="totpCode" type="text" inputmode="numeric" pattern="[0-9]*" />
            <button type="submit" [disabled]="confirmForm.invalid || loading()">Confirm and enable</button>
          </form>
          @if (error()) {
            <p class="error">{{ error() }}</p>
          }
        </div>
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
      form,
      .totp {
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
      .secret {
        word-break: break-all;
        background: #f5f5f5;
        padding: 0.5rem;
      }
    `,
  ],
})
export class SetupComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly sharedKey = signal<string | null>(null);
  readonly otpauthUri = signal<string | null>(null);
  // Angular blocks unknown URL schemes; this otpauth:// URI is minted by our own API.
  readonly otpauthLink = computed(() => {
    const uri = this.otpauthUri();
    return uri === null ? null : this.sanitizer.bypassSecurityTrustUrl(uri);
  });

  readonly form = this.fb.group({
    username: ['owner', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(12)]],
  });

  readonly confirmForm = this.fb.group({
    totpCode: ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
  });

  async onCreate(): Promise<void> {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const v = this.form.value;
    try {
      const result = await this.auth.setup(v.username!, v.password!);
      this.sharedKey.set(result.sharedKey);
      this.otpauthUri.set(result.otpauthUri);
    } catch (error: unknown) {
      this.error.set(describeSetupFailure(error));
    } finally {
      this.loading.set(false);
    }
  }

  async onConfirm(): Promise<void> {
    if (this.confirmForm.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const { username, password } = this.form.value;
    const totpCode = this.confirmForm.value.totpCode!;
    try {
      await this.auth.confirmTotp(username!, password!, totpCode);
      await this.router.navigate(['/login']);
    } catch {
      this.error.set('Invalid TOTP code. Try the next code from your app.');
    } finally {
      this.loading.set(false);
    }
  }
}

function describeSetupFailure(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Could not create the owner account. Please try again.';
  }
  switch (error.status) {
    case 0:
      return 'Cannot reach the Adesha API. Check that the backend is running.';
    case 400:
      return 'The password must be at least 12 characters.';
    case 409:
      return 'An owner already exists. Log in instead, or re-enter the original owner credentials to finish authenticator setup.';
    default:
      return 'Could not create the owner account. Check the API logs.';
  }
}

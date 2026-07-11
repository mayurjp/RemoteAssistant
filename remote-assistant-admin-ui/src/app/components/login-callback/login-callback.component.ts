import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login-callback',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="callback-container">
      <div class="card glass">
        <div *ngIf="loading" class="state-loading">
          <div class="spinner"></div>
          <h2>Signing You In</h2>
          <p>Verifying identity with Google...</p>
        </div>

        <div *ngIf="error" class="state-error">
          <span class="icon">⚠️</span>
          <h2>Login Failed</h2>
          <p class="error-msg">{{ errorMessage }}</p>
          <a routerLink="/login" class="btn btn-primary">Back to Login</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .callback-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      background: radial-gradient(circle at 50% 50%, #1e1b29 0%, #0d0c12 100%);
      color: #f1f1f1;
      font-family: 'Inter', system-ui, sans-serif;
      padding: 20px;
    }

    .card {
      background: rgba(30, 30, 46, 0.45);
      backdrop-filter: blur(12px);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 16px;
      padding: 40px;
      max-width: 500px;
      width: 100%;
      box-shadow: 0 8px 32px 0 rgba(0, 0, 0, 0.3);
      text-align: center;
    }

    .state-loading, .state-error {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
    }

    .spinner {
      width: 50px;
      height: 50px;
      border: 3px solid rgba(255, 255, 255, 0.1);
      border-top-color: #00f2fe;
      border-radius: 50%;
      animation: spin 1s linear infinite;
    }

    .icon {
      font-size: 40px;
      width: 70px;
      height: 70px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: bold;
    }

    .state-error .icon {
      background: rgba(229, 62, 62, 0.15);
      color: #f56565;
      border: 1px solid rgba(229, 62, 62, 0.3);
    }

    h2 {
      font-size: 22px;
      font-weight: 600;
      margin: 0;
    }

    p {
      color: #a0aec0;
      font-size: 14px;
      margin: 0;
      line-height: 1.5;
    }

    .error-msg {
      background: rgba(229, 62, 62, 0.08);
      border: 1px solid rgba(229, 62, 62, 0.2);
      border-radius: 8px;
      padding: 12px;
      font-size: 13px;
      color: #f56565;
      word-break: break-all;
    }

    .btn {
      text-decoration: none;
      border-radius: 8px;
      padding: 12px 24px;
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
      display: inline-block;
      margin-top: 10px;
      border: none;
    }

    .btn-primary {
      background: linear-gradient(135deg, #8a2be2 0%, #4a00e0 100%);
      color: #fff;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class LoginCallbackComponent implements OnInit {
  loading = true;
  error = false;
  errorMessage = '';

  constructor(
    private route: ActivatedRoute,
    private authService: AuthService
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      const code = params['code'];
      const errorParam = params['error'];

      if (errorParam) {
        this.loading = false;
        this.error = true;
        this.errorMessage = 'Login cancelled or rejected: ' + errorParam;
        return;
      }

      if (!code) {
        this.loading = false;
        this.error = true;
        this.errorMessage = 'No authorization code found in the callback URL.';
        return;
      }

      this.exchangeCode(code);
    });
  }

  exchangeCode(code: string) {
    this.authService.login(code, environment.loginRedirectUri).subscribe({
      error: (err) => {
        this.loading = false;
        this.error = true;
        this.errorMessage = err.error?.message || err.message || 'Token exchange failed.';
      }
    });
  }
}

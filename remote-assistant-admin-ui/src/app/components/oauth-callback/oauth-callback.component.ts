import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="callback-container">
      <div class="card glass">
        <div *ngIf="loading" class="state-loading">
          <div class="spinner"></div>
          <h2>Completing Gmail Authorization</h2>
          <p>Exchanging credentials with Google Security Services...</p>
        </div>

        <div *ngIf="success" class="state-success">
          <span class="icon">✓</span>
          <h2>Authorization Complete!</h2>
          <p>Gmail API integration is active. Re-routing to Dashboard...</p>
        </div>

        <div *ngIf="error" class="state-error">
          <span class="icon">⚠️</span>
          <h2>Authentication Failed</h2>
          <p class="error-msg">{{ errorMessage }}</p>
          <a routerLink="/setup" class="btn btn-primary">Try Again</a>
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

    .state-loading, .state-success, .state-error {
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

    .state-success .icon {
      background: rgba(72, 187, 120, 0.15);
      color: #48bb78;
      border: 1px solid rgba(72, 187, 120, 0.3);
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
      font-family: monospace;
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
export class OAuthCallbackComponent implements OnInit {
  loading = true;
  success = false;
  error = false;
  errorMessage = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      const code = params['code'];
      const errorParam = params['error'];

      if (errorParam) {
        this.handleFailure('OAuth login cancelled or rejected by user: ' + errorParam);
        return;
      }

      if (!code) {
        this.handleFailure('No authorization code found in redirect URL.');
        return;
      }

      this.exchangeCode(code);
    });
  }

  exchangeCode(code: string) {
    this.apiService.processOAuthCallback(code).subscribe({
      next: (res) => {
        this.loading = false;
        this.success = true;
        setTimeout(() => {
          this.router.navigate(['/dashboard']);
        }, 2500);
      },
      error: (err) => {
        this.handleFailure(err.error?.message || err.message || 'Token exchange failed.');
      }
    });
  }

  handleFailure(message: string) {
    this.loading = false;
    this.error = true;
    this.errorMessage = message;
  }
}

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="login-container">
      <div class="card glass">
        <div class="brand">
          <span class="logo">⚡</span>
          <h1>RemoteAssistant</h1>
        </div>

        <ng-container *ngIf="!credentialsConfigured && !saving">
          <p class="subtitle">First-run setup — enter your Google OAuth credentials</p>

          <div class="form-group">
            <label for="clientId">Client ID</label>
            <input
              type="text"
              id="clientId"
              [(ngModel)]="googleClientId"
              placeholder="xxxxx.apps.googleusercontent.com"
              class="form-control"
            />
          </div>

          <div class="form-group">
            <label for="clientSecret">Client Secret</label>
            <input
              type="password"
              id="clientSecret"
              [(ngModel)]="googleClientSecret"
              placeholder="GOCSPX-xxxxxxxxxxxxxxxxx"
              class="form-control"
            />
          </div>

          <button (click)="saveCredentials()" [disabled]="saving" class="btn btn-primary">
            {{ saving ? 'Saving...' : 'Save Credentials' }}
          </button>

          <p class="error-msg" *ngIf="error">{{ error }}</p>
          <p class="success-msg" *ngIf="successMsg">{{ successMsg }}</p>
        </ng-container>

        <ng-container *ngIf="credentialsConfigured || saving">
          <p class="subtitle">Sign in to access the admin panel</p>

          <button (click)="startLogin()" [disabled]="loading" class="btn btn-google">
            <svg class="google-icon" viewBox="0 0 24 24" width="20" height="20">
              <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z"/>
              <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
              <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
              <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
            </svg>
            {{ loading ? 'Loading...' : 'Sign in with Google' }}
          </button>

          <p class="error-msg" *ngIf="error">{{ error }}</p>
          <p class="footer-note">Only authorized Google accounts can access this panel.</p>
        </ng-container>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      background: radial-gradient(circle at 50% 50%, #1e1b29 0%, #0d0c12 100%);
      padding: 20px;
      font-family: 'Inter', system-ui, sans-serif;
    }

    .card {
      background: rgba(30, 30, 46, 0.45);
      backdrop-filter: blur(16px);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 20px;
      padding: 48px 40px;
      max-width: 460px;
      width: 100%;
      box-shadow: 0 12px 40px rgba(0, 0, 0, 0.4);
      text-align: center;
    }

    .brand {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      margin-bottom: 8px;
    }

    .logo {
      font-size: 48px;
    }

    h1 {
      font-size: 28px;
      font-weight: 700;
      color: #f1f1f1;
      margin: 0;
      letter-spacing: -0.5px;
    }

    .subtitle {
      color: #a0aec0;
      font-size: 15px;
      margin: 0 0 24px 0;
    }

    .form-group {
      margin-bottom: 16px;
      text-align: left;
    }

    label {
      display: block;
      font-size: 13px;
      font-weight: 600;
      color: #cbd5e0;
      margin-bottom: 6px;
    }

    .form-control {
      width: 100%;
      padding: 12px 14px;
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 10px;
      color: #f1f1f1;
      font-size: 14px;
      box-sizing: border-box;
      transition: border-color 0.2s;
    }

    .form-control:focus {
      outline: none;
      border-color: rgba(138, 43, 226, 0.6);
    }

    .form-control::placeholder {
      color: #6b7280;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      border: none;
      border-radius: 12px;
      padding: 14px 32px;
      font-size: 16px;
      font-weight: 600;
      cursor: pointer;
      width: 100%;
      transition: transform 0.15s, box-shadow 0.15s;
    }

    .btn:active {
      transform: scale(0.98);
    }

    .btn-google {
      background: #ffffff;
      color: #1a1a2e;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.3);
    }

    .btn-google:hover {
      box-shadow: 0 6px 24px rgba(0, 0, 0, 0.4);
    }

    .btn-primary {
      background: linear-gradient(135deg, #8a2be2 0%, #4a00e0 100%);
      color: #fff;
      box-shadow: 0 4px 15px rgba(138, 43, 226, 0.25);
    }

    .btn-primary:hover {
      transform: translateY(-1px);
      box-shadow: 0 6px 20px rgba(138, 43, 226, 0.4);
    }

    .google-icon {
      flex-shrink: 0;
    }

    .error-msg {
      color: #f56565;
      font-size: 13px;
      margin-top: 16px;
      margin-bottom: 0;
      text-align: center;
    }

    .success-msg {
      color: #48bb78;
      font-size: 13px;
      margin-top: 16px;
      margin-bottom: 0;
      text-align: center;
    }

    .footer-note {
      color: #6b7280;
      font-size: 12px;
      margin-top: 24px;
      margin-bottom: 0;
    }
  `]
})
export class LoginComponent implements OnInit {
  loading = false;
  saving = false;
  error = '';
  successMsg = '';
  credentialsConfigured = false;

  googleClientId = '';
  googleClientSecret = '';

  constructor(private apiService: ApiService) {}

  ngOnInit() {
    this.apiService.getConfigStatus().subscribe({
      next: (status) => {
        this.credentialsConfigured = !!(status.googleClientId || status.hasGoogleClientId);
      },
      error: () => {
        this.credentialsConfigured = false;
      }
    });
  }

  saveCredentials() {
    this.error = '';
    this.successMsg = '';

    if (!this.googleClientId.trim() || !this.googleClientSecret.trim()) {
      this.error = 'Both Client ID and Client Secret are required.';
      return;
    }

    this.saving = true;
    this.apiService.saveGoogleCredentials(this.googleClientId, this.googleClientSecret).subscribe({
      next: () => {
        this.saving = false;
        this.successMsg = 'Credentials saved! You may now sign in.';
        this.credentialsConfigured = true;
      },
      error: (err) => {
        this.saving = false;
        this.error = 'Failed to save: ' + (err.error?.message || err.message);
      }
    });
  }

  startLogin() {
    window.location.href = `${this.apiService.baseUrl}/auth/google-login`;
  }
}

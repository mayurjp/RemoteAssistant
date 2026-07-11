import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService, ConfigStatus } from '../../services/api.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-setup',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './setup.component.html',
  styleUrls: ['./setup.component.css']
})
export class SetupComponent implements OnInit {
  status: ConfigStatus = {
    hasGoogleClientId: false,
    hasGoogleClientSecret: false,
    hasGoogleRefreshToken: false,
    hasTelegramBotToken: false,
    googleAdminEmail: null,
    googleClientId: null
  };

  telegramToken: string = '';
  googleClientId: string = environment.googleClientId;
  googleClientSecret: string = environment.googleClientSecret;

  telegramMessage: string = '';
  telegramError: boolean = false;
  googleMessage: string = '';
  googleError: boolean = false;

  constructor(private apiService: ApiService) {}

  ngOnInit() {
    this.loadStatus();
  }

  loadStatus() {
    this.apiService.getConfigStatus().subscribe({
      next: (status) => {
        this.status = status;
      },
      error: (err) => {
        console.error('Failed to load status', err);
      }
    });
  }

  saveTelegram() {
    this.telegramMessage = '';
    if (!this.telegramToken.trim()) {
      this.telegramMessage = 'Please enter a valid token.';
      this.telegramError = true;
      return;
    }

    this.apiService.saveTelegramToken(this.telegramToken).subscribe({
      next: (res) => {
        this.telegramMessage = 'Telegram Token saved successfully!';
        this.telegramError = false;
        this.telegramToken = '';
        this.loadStatus();
      },
      error: (err) => {
        this.telegramMessage = 'Failed to save token: ' + (err.error?.message || err.message);
        this.telegramError = true;
      }
    });
  }

  saveGoogle() {
    this.googleMessage = '';
    if (!this.googleClientId.trim() || !this.googleClientSecret.trim()) {
      this.googleMessage = 'Both Client ID and Client Secret are required.';
      this.googleError = true;
      return;
    }

    this.apiService.saveGoogleCredentials(this.googleClientId, this.googleClientSecret).subscribe({
      next: (res) => {
        this.googleMessage = 'Google credentials saved!';
        this.googleError = false;
        this.loadStatus();
      },
      error: (err) => {
        this.googleMessage = 'Failed to save credentials: ' + (err.error?.message || err.message);
        this.googleError = true;
      }
    });
  }

  startOAuthRedirect() {
    const clientId = this.googleClientId.trim() || this.status.googleClientId || environment.googleClientId;
    if (!clientId) {
      this.googleMessage = 'Google Client ID is not configured. Check the environment file.';
      this.googleError = true;
      return;
    }

    const googleAuthUrl = 'https://accounts.google.com/o/oauth2/v2/auth';
    const params = new URLSearchParams({
      client_id: clientId,
      redirect_uri: environment.oauthRedirectUri,
      response_type: 'code',
      scope: 'openid email https://www.googleapis.com/auth/gmail.send',
      access_type: 'offline',
      prompt: 'consent'
    });

    window.location.href = `${googleAuthUrl}?${params.toString()}`;
  }
}

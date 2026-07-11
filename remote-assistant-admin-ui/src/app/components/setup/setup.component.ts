import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService, ConfigStatus } from '../../services/api.service';

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
    googleAdminEmail: null
  };

  telegramToken: string = '';
  googleClientId: string = '';
  googleClientSecret: string = '';

  telegramMessage: string = '';
  telegramError: boolean = false;
  googleMessage: string = '';
  googleError: boolean = false;

  constructor(private apiService: ApiService, private router: Router) {}

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
        this.googleMessage = 'Google credentials saved! You can now authorize Gmail below.';
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
    // If the form fields are filled we use them, otherwise we expect they are already saved in DB
    const clientId = this.googleClientId.trim() || '';
    
    // Construct Google OAuth URL
    const googleAuthUrl = 'https://accounts.google.com/o/oauth2/v2/auth';
    const params = new URLSearchParams({
      client_id: clientId || 'RETRIEVE_FROM_BACKEND', // we will pass it dynamically or redirect
      redirect_uri: 'http://localhost:4200/oauth-callback',
      response_type: 'code',
      scope: 'openid email https://www.googleapis.com/auth/gmail.send',
      access_type: 'offline',
      prompt: 'consent'
    });

    if (!clientId) {
      // If client ID is already saved in DB but not in form, we need WebApi to give it or we can query status
      this.apiService.getConfigStatus().subscribe(status => {
        // Unfortunately backend doesn't return the raw client ID for security. 
        // Admin must specify it in the form or we can retrieve it safely if we had a dedicated endpoint.
        // For standard flow, the admin saves and immediately clicks authorize, so googleClientId is in the variable.
        if (this.googleClientId) {
          window.location.href = `${googleAuthUrl}?${params.toString()}`;
        } else {
          this.googleMessage = 'Please enter your Client ID in the field above to start authorization.';
          this.googleError = true;
        }
      });
    } else {
      window.location.href = `${googleAuthUrl}?${params.toString()}`;
    }
  }
}

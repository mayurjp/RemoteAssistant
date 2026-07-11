import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
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
    googleAdminEmail: null,
    googleClientId: null
  };

  telegramToken: string = '';

  telegramMessage: string = '';
  telegramError: boolean = false;

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
}

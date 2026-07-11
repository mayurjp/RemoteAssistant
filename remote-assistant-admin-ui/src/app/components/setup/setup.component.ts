import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService, ConfigStatus, TelegramBot } from '../../services/api.service';

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
    telegramBotCount: 0,
    googleAdminEmail: null
  };

  bots: TelegramBot[] = [];

  showForm = false;
  editingBotId: number | null = null;
  botName = '';
  botDescription = '';
  botToken = '';

  saving = false;
  botMessage = '';
  botError = false;

  constructor(private apiService: ApiService) {}

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.apiService.getConfigStatus().subscribe({
      next: (status) => this.status = status,
      error: (err) => console.error('Failed to load status', err)
    });

    this.apiService.getBots().subscribe({
      next: (bots) => this.bots = bots,
      error: (err) => console.error('Failed to load bots', err)
    });
  }

  showAddForm() {
    this.showForm = true;
    this.editingBotId = null;
    this.botName = '';
    this.botDescription = '';
    this.botToken = '';
    this.botMessage = '';
  }

  editBot(bot: TelegramBot) {
    this.showForm = true;
    this.editingBotId = bot.id;
    this.botName = bot.name;
    this.botDescription = bot.description || '';
    this.botToken = bot.token;
    this.botMessage = '';
  }

  cancelForm() {
    this.showForm = false;
    this.editingBotId = null;
    this.botMessage = '';
  }

  saveBot() {
    this.botMessage = '';

    if (!this.botName.trim() || !this.botToken.trim()) {
      this.botMessage = 'Name and Token are required.';
      this.botError = true;
      return;
    }

    const request = {
      name: this.botName.trim(),
      description: this.botDescription.trim() || undefined,
      token: this.botToken.trim()
    };

    this.saving = true;

    const action = this.editingBotId
      ? this.apiService.updateBot(this.editingBotId, request)
      : this.apiService.createBot(request);

    action.subscribe({
      next: () => {
        this.botMessage = this.editingBotId ? 'Bot updated successfully!' : 'Bot added successfully!';
        this.botError = false;
        this.saving = false;
        this.cancelForm();
        this.loadData();
      },
      error: (err) => {
        this.botMessage = 'Failed: ' + (err.error?.message || err.message);
        this.botError = true;
        this.saving = false;
      }
    });
  }

  toggleBot(bot: TelegramBot) {
    this.apiService.toggleBot(bot.id).subscribe({
      next: () => this.loadData(),
      error: (err) => console.error('Failed to toggle bot', err)
    });
  }

  deleteBot(id: number) {
    if (!confirm('Are you sure you want to delete this bot?')) return;

    this.apiService.deleteBot(id).subscribe({
      next: () => {
        this.botMessage = 'Bot deleted.';
        this.botError = false;
        this.loadData();
      },
      error: (err) => {
        this.botMessage = 'Failed to delete: ' + (err.error?.message || err.message);
        this.botError = true;
      }
    });
  }
}

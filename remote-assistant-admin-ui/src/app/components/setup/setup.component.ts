import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService, ConfigStatus, TelegramBot, UserMembership, RegistrationRequest, JobTypeInfo } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

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
  registrations: { [botId: number]: UserMembership[] } = {};
  pending: { [botId: number]: RegistrationRequest[] } = {};
  expandedBotId: number | null = null;

  jobTypes: JobTypeInfo[] = [];
  showJobPanel = false;
  editingBotJobId: number | null = null;
  botJobTypes: number[] = [];

  showForm = false;
  editingBotId: number | null = null;
  botName = '';
  botDescription = '';
  botToken = '';

  saving = false;
  botMessage = '';
  botError = false;

  constructor(private apiService: ApiService, private authService: AuthService) {}

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

    this.apiService.getJobTypes().subscribe({
      next: (types) => this.jobTypes = types,
      error: (err) => console.error('Failed to load job types', err)
    });
  }

  toggleRegistrations(botId: number) {
    if (this.expandedBotId === botId) {
      this.expandedBotId = null;
      return;
    }
    this.expandedBotId = botId;
    this.apiService.getBotRegistrations(botId).subscribe({
      next: (regs) => this.registrations[botId] = regs,
      error: (err) => console.error('Failed to load registrations', err)
    });
    this.apiService.getRegistrationRequests(botId).subscribe({
      next: (pending) => this.pending[botId] = pending,
      error: (err) => console.error('Failed to load pending registrations', err)
    });
  }

  approveRegistration(botId: number, id: number) {
    this.apiService.approveRegistration(botId, id).subscribe({
      next: () => this.toggleRegistrations(botId),
      error: (err) => alert('Failed: ' + (err.error?.message || err.message))
    });
    this.toggleRegistrations(botId);
  }

  rejectRegistration(botId: number, id: number) {
    this.apiService.rejectRegistration(botId, id).subscribe({
      next: () => this.refreshExpand(botId),
      error: (err) => alert('Failed: ' + (err.error?.message || err.message))
    });
  }

  reapproveRegistration(botId: number, id: number) {
    this.apiService.reapproveRegistration(botId, id).subscribe({
      next: () => this.refreshExpand(botId),
      error: (err) => alert('Failed: ' + (err.error?.message || err.message))
    });
  }

  unregisterUser(botId: number, regId: number) {
    if (!confirm('Unregister this user? They will be notified and can re-register.')) return;
    this.apiService.unregisterUser(botId, regId).subscribe({
      next: () => this.refreshExpand(botId),
      error: (err) => alert('Failed: ' + (err.error?.message || err.message))
    });
  }

  private refreshExpand(botId: number) {
    this.apiService.getBotRegistrations(botId).subscribe({
      next: (regs) => this.registrations[botId] = regs,
      error: (err) => console.error(err)
    });
    this.apiService.getRegistrationRequests(botId).subscribe({
      next: (pending) => this.pending[botId] = pending,
      error: (err) => console.error(err)
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

  openJobAssignment(bot: TelegramBot) {
    this.editingBotJobId = bot.id;
    this.showJobPanel = true;
    this.apiService.getBotJobs(bot.id).subscribe({
      next: (types) => this.botJobTypes = [...types],
      error: () => this.botJobTypes = []
    });
  }

  toggleBotJobType(jobId: number) {
    const idx = this.botJobTypes.indexOf(jobId);
    if (idx >= 0) this.botJobTypes.splice(idx, 1);
    else this.botJobTypes.push(jobId);
  }

  saveBotJobs() {
    if (this.editingBotJobId == null) return;
    this.apiService.setBotJobs(this.editingBotJobId, this.botJobTypes).subscribe({
      next: () => {
        this.showJobPanel = false;
        this.editingBotJobId = null;
        this.loadData();
      },
      error: (err) => alert('Failed: ' + (err.error?.message || err.message))
    });
  }

  logout() {
    this.authService.logout();
  }
}

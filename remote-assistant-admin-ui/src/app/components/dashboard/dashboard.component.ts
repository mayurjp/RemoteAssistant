import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService, ConfigStatus, User } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  status: ConfigStatus = {
    hasGoogleClientId: false,
    hasGoogleClientSecret: false,
    hasGoogleRefreshToken: false,
    hasTelegramBotToken: false,
    googleAdminEmail: null
  };

  users: User[] = [];
  filteredUsers: User[] = [];
  searchQuery: string = '';
  loading: boolean = true;

  userEmail: string | null = null;

  constructor(
    private apiService: ApiService,
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit() {
    this.userEmail = this.authService.getEmail();
    this.loadData();
  }

  loadData() {
    this.loading = true;
    this.apiService.getConfigStatus().subscribe({
      next: (status) => {
        this.status = status;
      },
      error: (err) => {
        console.error('Failed to load status', err);
      }
    });

    this.apiService.getUsers().subscribe({
      next: (users) => {
        this.users = users;
        this.applyFilter();
        this.loading = false;
      },
      error: (err) => {
        console.error('Failed to load users', err);
        this.loading = false;
      }
    });
  }

  applyFilter() {
    if (!this.searchQuery.trim()) {
      this.filteredUsers = [...this.users];
      return;
    }

    const query = this.searchQuery.toLowerCase().trim();
    this.filteredUsers = this.users.filter(user => 
      user.email.toLowerCase().includes(query) || 
      user.telegramId.toString().includes(query)
    );
  }

  get isConfigured(): boolean {
    return this.status.hasTelegramBotToken;
  }

  logout() {
    this.authService.logout();
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ConfigStatus {
  hasGoogleClientId: boolean;
  hasGoogleClientSecret: boolean;
  hasGoogleRefreshToken: boolean;
  hasTelegramBotToken: boolean;
  telegramBotCount: number;
  googleAdminEmail: string | null;
}

export interface User {
  telegramId: number;
  email: string;
  isVerified: boolean;
  otpCode: string | null;
  otpExpiry: string | null;
  createdAt: string;
  verifiedAt: string | null;
}

export interface TelegramBot {
  id: number;
  name: string;
  description: string | null;
  token: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface TelegramBotRequest {
  name: string;
  description?: string;
  token: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  baseUrl = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  getConfigStatus(): Observable<ConfigStatus> {
    return this.http.get<ConfigStatus>(`${this.baseUrl}/config`);
  }

  saveTelegramToken(token: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/config/telegram`, { token });
  }

  saveGoogleCredentials(clientId: string, clientSecret: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/config/google`, { clientId, clientSecret });
  }

  getUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.baseUrl}/users`);
  }

  getBots(): Observable<TelegramBot[]> {
    return this.http.get<TelegramBot[]>(`${this.baseUrl}/bots`);
  }

  createBot(bot: TelegramBotRequest): Observable<TelegramBot> {
    return this.http.post<TelegramBot>(`${this.baseUrl}/bots`, bot);
  }

  updateBot(id: number, bot: TelegramBotRequest): Observable<TelegramBot> {
    return this.http.put<TelegramBot>(`${this.baseUrl}/bots/${id}`, bot);
  }

  toggleBot(id: number): Observable<TelegramBot> {
    return this.http.patch<TelegramBot>(`${this.baseUrl}/bots/${id}/toggle`, {});
  }

  deleteBot(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/bots/${id}`);
  }
}

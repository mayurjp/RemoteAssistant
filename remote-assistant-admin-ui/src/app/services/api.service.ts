import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ConfigStatus {
  hasGoogleClientId: boolean;
  hasGoogleClientSecret: boolean;
  hasGoogleRefreshToken: boolean;
  hasTelegramBotToken: boolean;
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

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private baseUrl = 'http://localhost:5000/api/admin';

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

  processOAuthCallback(code: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/oauth/callback`, { code });
  }

  getUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.baseUrl}/users`);
  }
}

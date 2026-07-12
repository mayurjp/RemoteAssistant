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

export interface TelegramBot {
  id: number;
  name: string;
  description: string | null;
  token: string;
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

  getBots(): Observable<TelegramBot[]> {
    return this.http.get<TelegramBot[]>(`${this.baseUrl}/bots`);
  }

  createBot(bot: TelegramBotRequest): Observable<TelegramBot> {
    return this.http.post<TelegramBot>(`${this.baseUrl}/bots`, bot);
  }

  updateBot(id: number, bot: TelegramBotRequest): Observable<TelegramBot> {
    return this.http.put<TelegramBot>(`${this.baseUrl}/bots/${id}`, bot);
  }

  deleteBot(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/bots/${id}`);
  }

  getBotRegistrations(botId: number): Observable<UserMembership[]> {
    return this.http.get<UserMembership[]>(`${this.baseUrl}/bots/${botId}/registrations`);
  }

  getRegistrationRequests(botId: number): Observable<RegistrationRequest[]> {
    return this.http.get<RegistrationRequest[]>(`${this.baseUrl}/bots/${botId}/pending`);
  }

  approveRegistration(botId: number, id: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/bots/${botId}/pending/${id}/approve`, {});
  }

  rejectRegistration(botId: number, id: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/bots/${botId}/pending/${id}/reject`, {});
  }

  reapproveRegistration(botId: number, id: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/bots/${botId}/pending/${id}/reapprove`, {});
  }

  unregisterUser(botId: number, regId: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/bots/${botId}/registrations/${regId}/unregister`, {});
  }

  getJobTypes(): Observable<JobTypeInfo[]> {
    return this.http.get<JobTypeInfo[]>(`${this.baseUrl}/job-types`);
  }

  getBotJobs(botId: number): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/bots/${botId}/jobs`);
  }

  setBotJobs(botId: number, jobTemplateIds: number[]): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/bots/${botId}/jobs`, jobTemplateIds);
  }
}

export interface JobTypeInfo {
  id: number;
  jobType: string;
  name: string;
  description: string;
}

export interface UserMembership {
  id: number;
  telegramId: number;
  registeredAt: string;
}

export interface RegistrationRequest {
  id: number;
  telegramId: number;
  status: string;
  requestedAt: string;
  reviewedAt: string | null;
  reviewedBy: string | null;
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';

export interface AuthStatus {
  authenticated: boolean;
  email: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private baseUrl = environment.apiBaseUrl;
  private tokenKey = 'auth_token';
  private emailKey = 'auth_email';

  private authSubject = new BehaviorSubject<boolean>(this.hasToken());
  isAuthenticated$ = this.authSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    this.readCookie();
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getEmail(): string | null {
    return localStorage.getItem(this.emailKey);
  }

  isAuthenticated(): boolean {
    return this.hasToken();
  }

  private hasToken(): boolean {
    const token = this.getToken();
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }

  private readCookie(): void {
    const cookies = document.cookie.split(';');
    let token = '';
    let email = '';

    for (const cookie of cookies) {
      const [name, ...valueParts] = cookie.trim().split('=');
      const value = decodeURIComponent(valueParts.join('='));
      if (name === 'auth_token') token = value;
      if (name === 'auth_email') email = value;
    }

    if (token) {
      localStorage.setItem(this.tokenKey, token);
      localStorage.setItem(this.emailKey, email);
      document.cookie = 'auth_token=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;';
      document.cookie = 'auth_email=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;';
      this.authSubject.next(true);
    }
  }

  checkAuthStatus(): Observable<AuthStatus> {
    return this.http.get<AuthStatus>(`${this.baseUrl}/auth/status`).pipe(
      tap({
        error: () => this.logout()
      })
    );
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.emailKey);
    this.authSubject.next(false);
    this.router.navigate(['/login']);
  }
}

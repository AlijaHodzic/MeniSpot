import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_URL } from '../api.config';
import { AuthSession, LoginRequest, UserRole } from './auth.models';

const SESSION_KEY = 'menispot.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly sessionState = signal<AuthSession | null>(this.restoreSession());

  readonly session = this.sessionState.asReadonly();
  readonly isAuthenticated = computed(() => this.sessionState() !== null);
  readonly role = computed(() => this.sessionState()?.role ?? null);

  login(request: LoginRequest): Observable<AuthSession> {
    return this.http.post<AuthSession>(`${API_URL}/auth/login`, request).pipe(
      tap((session) => {
        localStorage.setItem(SESSION_KEY, JSON.stringify(session));
        this.sessionState.set(session);
      }),
    );
  }

  logout(): void {
    localStorage.removeItem(SESSION_KEY);
    this.sessionState.set(null);
    void this.router.navigate(['/auth/login']);
  }

  hasRole(roles: UserRole[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  dashboardUrl(session: AuthSession = this.sessionState()!): string[] {
    if (session.role === 'SuperAdmin') return ['/admin', 'dashboard'];
    return ['/restaurant', session.restaurantId ?? '', 'dashboard'];
  }

  private restoreSession(): AuthSession | null {
    const stored = localStorage.getItem(SESSION_KEY);
    if (!stored) return null;

    try {
      const session = JSON.parse(stored) as AuthSession;
      if (!session.accessToken || new Date(session.expiresAt).getTime() <= Date.now()) {
        localStorage.removeItem(SESSION_KEY);
        return null;
      }
      return session;
    } catch {
      localStorage.removeItem(SESSION_KEY);
      return null;
    }
  }
}

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map, tap } from 'rxjs';

import { SessionResponse } from './session-response';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private authenticated = false;
  private permissions: readonly string[] = [];

  constructor(private readonly http: HttpClient) {}

  login(username: string, password: string): Observable<SessionResponse> {
    return this.http
      .post<SessionResponse>('/api/auth/session', { username, password })
      .pipe(tap((response) => this.setSession(response)));
  }

  logout(): Observable<void> {
    return this.http
      .delete<void>('/api/auth/session')
      .pipe(tap(() => this.clearLocalSession()));
  }

  refreshSession(): Observable<boolean> {
    return this.http.get<SessionResponse>('/api/auth/session').pipe(
      tap((response) => this.setSession(response)),
      map(() => true),
    );
  }

  clearLocalSession(): void {
    this.authenticated = false;
    this.permissions = [];
  }

  hasSession(): boolean {
    return this.authenticated;
  }

  canWriteLoans(): boolean {
    return this.permissions.includes('loans.write');
  }

  private setSession(response: SessionResponse): void {
    this.authenticated = true;
    this.permissions = response.permissions;
  }
}

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { API_URL } from './config';
import { AuthApi } from './auth-api';

// Talks to our backend's /login. Knows nothing about storage or the UI.
@Injectable()
export class HttpAuthApi extends AuthApi {
  private http = inject(HttpClient);

  login(username: string, password: string): Observable<string> {
    return this.http
      .post<{ token: string }>(`${API_URL}/login`, { username, password })
      .pipe(map((res) => res.token));
  }
}

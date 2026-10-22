import { Injectable, computed, inject, signal } from '@angular/core';
import { tap } from 'rxjs';
import { AuthApi } from './auth-api';

const KEY = 'token';

// The session state: who holds a token right now. sessionStorage keeps it across
// reloads (not across tabs). Knows nothing about HTTP details or the UI.
@Injectable({ providedIn: 'root' })
export class Auth {
  private api = inject(AuthApi);

  readonly token = signal(sessionStorage.getItem(KEY));
  readonly loggedIn = computed(() => !!this.token());

  login(username: string, password: string) {
    return this.api.login(username, password).pipe(tap((token) => this.set(token)));
  }

  logout() {
    this.set(null);
  }

  private set(token: string | null) {
    this.token.set(token);
    token ? sessionStorage.setItem(KEY, token) : sessionStorage.removeItem(KEY);
  }
}

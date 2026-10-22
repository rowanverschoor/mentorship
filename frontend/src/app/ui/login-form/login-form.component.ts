import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Auth } from '../../core/auth';

@Component({
  selector: 'app-login-form',
  imports: [FormsModule],
  template: `
    <form (ngSubmit)="submit()">
      <input name="username" [(ngModel)]="username" placeholder="Username" required />
      <input name="password" [(ngModel)]="password" type="password" placeholder="Password" required />
      <button>Log in</button>
      @if (error()) { <p>{{ error() }}</p> }
    </form>
  `,
})
export class LoginFormComponent {
  private auth = inject(Auth);
  username = '';
  password = '';
  error = signal('');

  submit() {
    this.auth.login(this.username, this.password).subscribe({
      error: (e) =>
        this.error.set(e.status === 429 ? 'Too many attempts, try again in a minute' : 'Wrong username or password'),
    });
  }
}

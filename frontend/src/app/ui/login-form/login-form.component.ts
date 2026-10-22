import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Auth } from '../../core/auth';

@Component({
  selector: 'app-login-form',
  imports: [FormsModule],
  templateUrl: './login-form.component.html',
  styleUrl: './login-form.component.css',
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

import { Component, inject } from '@angular/core';
import { Auth } from './core/auth';
import { LoginFormComponent } from './ui/login-form/login-form.component';
import { WeatherPageComponent } from './pages/weather-page/weather-page.component';

@Component({
  selector: 'app-root',
  imports: [LoginFormComponent, WeatherPageComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {
  protected auth = inject(Auth);
}

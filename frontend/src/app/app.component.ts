import { Component } from '@angular/core';
import { WeatherPageComponent } from './pages/weather-page/weather-page.component';

@Component({
  selector: 'app-root',
  imports: [WeatherPageComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {}

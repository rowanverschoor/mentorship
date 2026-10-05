import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Weather } from './models/weather';
import { WeatherService } from './services/weather.service';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  template: `
    <h1>Weather</h1>

    <input type="number" [(ngModel)]="latitude" placeholder="Latitude" />
    <input type="number" [(ngModel)]="longitude" placeholder="Longitude" />
    <button (click)="load()">Get weather</button>

    @if (error) {
      <p>{{ error }}</p>
    }

    @if (weather) {
      <ul>
        <li>Temperature: {{ weather.temperature }}°C</li>
        <li>Feels like: {{ weather.feelsLike }}°C</li>
        <li>Humidity: {{ weather.humidity }}%</li>
        <li>Wind: {{ weather.windSpeed }} m/s</li>
        <li>Clouds: {{ weather.cloudPercentage }}%</li>
      </ul>
    }
  `,
})
export class AppComponent {
  private weatherService = inject(WeatherService);

  latitude = 52.37;
  longitude = 4.89;
  weather: Weather | null = null;
  error = '';

  load() {
    this.error = '';
    this.weatherService.get(this.latitude, this.longitude).subscribe({
      next: (weather) => (this.weather = weather),
      error: () => (this.error = 'Could not load weather'),
    });
  }
}
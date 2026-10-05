import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  template: `
    <input type="number" [(ngModel)]="latitude" placeholder="lat" />
    <input type="number" [(ngModel)]="longitude" placeholder="lon" />
    <button (click)="getWeather()">Get weather</button>
    <pre>{{ result }}</pre>
  `,
})
export class AppComponent {
  latitude = 52.37;
  longitude = 4.89;
  result = '';

  private http = inject(HttpClient);

  getWeather() {
    this.http
      .get('http://localhost:5297/weather', {
        params: { lat: this.latitude, lon: this.longitude },
      })
      .subscribe({
        next: (data) => (this.result = JSON.stringify(data, null, 2)),
        error: (err) => (this.result = 'Error: ' + err.message),
      });
  }
}

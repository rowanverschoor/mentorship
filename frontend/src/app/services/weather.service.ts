import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config';
import { Weather } from '../models/weather';

// Talks to our backend. Knows nothing about the UI.
@Injectable({ providedIn: 'root' })
export class WeatherService {
  private http = inject(HttpClient);

  get(latitude: number, longitude: number): Observable<Weather> {
    return this.http.get<Weather>(`${API_URL}/weather`, {
      params: { lat: latitude, lon: longitude },
    });
  }
}
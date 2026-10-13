import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from './config';
import { Weather } from '../models/weather';
import { WeatherApi } from './weather-api';

// Talks to our backend over HTTP. Knows nothing about the UI.
@Injectable()
export class HttpWeatherApi extends WeatherApi {
  private http = inject(HttpClient);

  get(latitude: number, longitude: number): Observable<Weather> {
    return this.http.get<Weather>(`${API_URL}/weather`, {
      params: { lat: latitude, lon: longitude },
    });
  }
}

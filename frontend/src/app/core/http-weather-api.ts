import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, first, from, map, switchMap, timeout } from 'rxjs';
import { API_URL } from './config';
import { Weather } from '../models/weather';
import { WeatherApi } from './weather-api';
import { WeatherHub } from './weather-hub';

// Queues a request over HTTP and waits for the result on the hub. Knows nothing about the UI.
@Injectable()
export class HttpWeatherApi extends WeatherApi {
  private http = inject(HttpClient);
  private hub = inject(WeatherHub);

  get(latitude: number, longitude: number): Observable<Weather> {
    return from(this.hub.connected()).pipe(
      // Connected first, so the result has somewhere to go.
      switchMap(() =>
        this.http.post<{ id: string }>(`${API_URL}/weather`, null, { params: { lat: latitude, lon: longitude } }),
      ),
      // The POST only returns a receipt; the result arrives on the hub with the same id.
      switchMap(({ id }) => this.hub.updates$.pipe(first((update) => update.id === id))),
      // The server gives up after about 30s; never wait forever if a push gets lost.
      timeout(60_000),
      map((update) => {
        if (!update.weather) throw new Error(update.error ?? 'No weather');
        return update.weather;
      }),
    );
  }
}

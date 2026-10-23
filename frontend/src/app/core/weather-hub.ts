import { Injectable, effect, inject } from '@angular/core';
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { ReplaySubject } from 'rxjs';
import { API_URL } from './config';
import { Auth } from './auth';
import { Weather } from '../models/weather';

// Shape of what the backend pushes (WeatherUpdate in C#).
export interface WeatherUpdate {
  id: string;
  weather: Weather | null;
  error: string | null;
}

// The live connection to the backend. Receives finished jobs; knows nothing about the UI.
@Injectable({ providedIn: 'root' })
export class WeatherHub {
  private auth = inject(Auth);

  // Replays recent pushes: a fast result can arrive before the POST that started it has answered.
  readonly updates$ = new ReplaySubject<WeatherUpdate>(20, 30_000);

  private connection = new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/weather`, { accessTokenFactory: () => this.auth.token() ?? '' })
    .withAutomaticReconnect()
    .build();

  private starting = Promise.resolve();

  constructor() {
    this.connection.on('weather', (update: WeatherUpdate) => this.updates$.next(update));

    // A connection belongs to whoever logged in; close it on logout so the next login gets its own.
    effect(() => {
      if (!this.auth.loggedIn()) this.connection.stop();
    });
  }

  // Starts the connection on first use. Every caller waits for the same start.
  connected(): Promise<void> {
    if (this.connection.state === HubConnectionState.Disconnected) this.starting = this.connection.start();
    return this.starting;
  }
}

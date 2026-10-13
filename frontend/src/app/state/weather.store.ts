import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { Weather } from '../models/weather';
import { WeatherApi } from '../core/weather-api';

interface Location {
  latitude: number;
  longitude: number;
}

interface WeatherState {
  weather: Weather | null;
  loading: boolean;
  error: string;
}

const INITIAL: WeatherState = { weather: null, loading: false, error: '' };

// Reactive: requests flow in as a stream, state flows out as a signal.
// Nobody sets state by hand; it is always derived from the stream.
// Knows nothing about the UI.
@Injectable({ providedIn: 'root' })
export class WeatherStore {
  private api = inject(WeatherApi);

  private requests$ = new Subject<Location>();

  private state = toSignal(
    this.requests$.pipe(
      // A new request cancels the one still in flight, so a slow old answer
      // can never overwrite a newer one.
      switchMap(({ latitude, longitude }) =>
        this.api.get(latitude, longitude).pipe(
          map((weather): WeatherState => ({ weather, loading: false, error: '' })),
          catchError(() => of<WeatherState>({ weather: null, loading: false, error: 'Could not load weather' })),
          startWith<WeatherState>({ weather: null, loading: true, error: '' }),
        ),
      ),
    ),
    { initialValue: INITIAL },
  );

  readonly weather = computed(() => this.state().weather);
  readonly loading = computed(() => this.state().loading);
  readonly error = computed(() => this.state().error);

  load(latitude: number, longitude: number) {
    this.requests$.next({ latitude, longitude });
  }
}

import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { WeatherApi } from './core/weather-api';
import { HttpWeatherApi } from './core/http-weather-api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(),
    // The only place that decides which WeatherApi implementation is used.
    { provide: WeatherApi, useClass: HttpWeatherApi },
  ],
};

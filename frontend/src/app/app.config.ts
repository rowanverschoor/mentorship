import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/auth.interceptor';
import { WeatherApi } from './core/weather-api';
import { HttpWeatherApi } from './core/http-weather-api';
import { AuthApi } from './core/auth-api';
import { HttpAuthApi } from './core/http-auth-api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withInterceptors([authInterceptor])),
    // The only place that decides which API implementations are used.
    { provide: WeatherApi, useClass: HttpWeatherApi },
    { provide: AuthApi, useClass: HttpAuthApi },
  ],
};

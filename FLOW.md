# Program flow

Paths are relative to `frontend/src/app/` or `backend/WeatherApi/`.

The app runs in two ways. The code is identical; only what sits between the browser and the API differs:

| | Development (`./run.sh`) | Docker (`docker compose up`) |
|---|---|---|
| Frontend served by | `ng serve` on :4200 | nginx on :4200 |
| `/api` forwarded by | `ng serve` proxy (`frontend/proxy.conf.json`) to `localhost:5149` | nginx (`frontend/nginx.conf`) to `api:8080` |
| API key from | user-secrets | `API_NINJAS_KEY` in `.env` → `ApiNinjas__Key` |

## A. Startup

### Backend (`Program.cs`, top to bottom)

1. `WebApplication.CreateBuilder` loads config. The API key comes from `ApiNinjas:Key` (user-secrets, or the `ApiNinjas__Key` environment variable in Docker).
2. `AddHttpClient<IWeatherService, ApiNinjasWeatherService>(...)` registers the provider. It sets the base URL and the `X-Api-Key` header.
3. `.AddWeatherResilience()` (`Resilience/ResilienceExtensions.cs`) builds the retry and circuit-breaker pipeline around that client.
4. `AddWeatherRateLimiting()` (`Resilience/RateLimitExtensions.cs`) registers the per-IP limiter policy named `"weather"`.
5. `Configure<ForwardedHeadersOptions>` makes the API trust `X-Forwarded-For`, so the rate limiter sees the real client IP instead of the proxy's.
6. `app.UseForwardedHeaders()`, then `app.UseRateLimiter()`, then `MapGet("/weather", ...).RequireRateLimiting("weather")`.
7. `app.Run()` listens on `localhost:5149` (development) or `:8080` (Docker).

There is no CORS setup: the browser never calls the API directly (see step B4).

### Frontend

1. `main.ts` calls `bootstrapApplication(AppComponent, appConfig)`.
2. `app.config.ts` registers `provideHttpClient()` and `{ provide: WeatherApi, useClass: HttpWeatherApi }`. This is the one place that picks the implementation.
3. `app.component.ts` renders `app.component.html`, which contains `<app-weather-page />`.
4. `pages/weather-page/weather-page.component.ts` injects `WeatherStore` (`state/weather.store.ts`). The store is created, and in its field initialisers it injects `WeatherApi` (which resolves to `HttpWeatherApi`). It also builds the `requests$` stream and `toSignal(...)`, which subscribes right away.

## B. One request, step by step

### 1. Browser: user input (`ui/location-form/`)

- `location-form.component.html`: the user edits two inputs, bound with `[(ngModel)]` to `latitude` and `longitude`. They click "Get weather", which calls `submit()`.
- `location-form.component.ts` → `submit()`: `submitted.emit({ latitude, longitude })` (a `Location`, `models/location.ts`).

### 2. Browser: page wires it to the store (`pages/weather-page/`)

- `weather-page.component.html`: `(submitted)="store.load($event.latitude, $event.longitude)"`. The page has no logic, it only passes the event on.

### 3. Browser: store (`state/weather.store.ts`)

- `load(lat, lon)` → `requests$.next({ latitude, longitude })`.
- The stream reaches `switchMap(...)`, which cancels any request still in flight and calls `api.get(lat, lon)`.
- `startWith` first emits `{ loading: true }`, so the UI shows "Loading..." immediately.
- On success, `map` produces `{ weather, loading: false, error: '' }`. On failure, `catchError` produces `{ weather: null, loading: false, error: 'Could not load weather' }`.
- `toSignal` stores the latest state. The `weather`, `loading` and `error` signals are `computed` from it.

### 4. Browser → proxy (`core/http-weather-api.ts`)

- `WeatherApi.get` (the abstract contract in `core/weather-api.ts`) is implemented by `HttpWeatherApi.get()`. It sends `GET /api/weather?lat=..&lon=..` to **its own origin** (`:4200`). `API_URL` is `'/api'` (`core/config.ts`).
- The proxy forwards it and removes the `/api` prefix:
  - development: `ng serve` → `http://localhost:5149/weather?...`
  - Docker: nginx → `http://api:8080/weather?...`, and sets `X-Forwarded-For` to the browser's IP.
- Because the browser only talks to `:4200`, this is not a cross-origin request and no CORS is needed.

### 5. Backend: middleware order (`Program.cs`)

- `UseForwardedHeaders` reads the real client IP from `X-Forwarded-For`.
- `UseRateLimiter` counts the request for that IP (20 per minute; the 21st gets 429).

### 6. Backend: endpoint (`Program.cs`, the `MapGet` lambda)

- It checks that lat is in ±90 and lon is in ±180. If not, it returns 400.
- Otherwise it calls `weather.GetAsync(lat, lon)` on `IWeatherService` (`Services/IWeatherService.cs`). That resolves to `ApiNinjasWeatherService`, so the endpoint never names the provider.

### 7. Backend: provider (`Services/ApiNinjasWeatherService.cs` → `GetAsync`)

- It builds `v1/weather?lat=..&lon=..` with invariant formatting.
- `http.GetFromJsonAsync<ApiNinjasResponse>(url)` sends the call. The HTTP client goes through the resilience pipeline first:
  - **total timeout (30s)** → **retry (up to 3 retries, backoff, jitter)** → **circuit breaker** → **per-attempt timeout (5s)** → real HTTP call to `api.api-ninjas.com`.
- The private record `ApiNinjasResponse` maps API Ninjas' JSON (`temp`, `feels_like`, `wind_speed`, `cloud_pct`).
- It returns `new WeatherResult(...)` (`Models/WeatherResult.cs`).

### 8. Backend: response

- The endpoint returns `Results.Ok(result)` as JSON, camelCase: `{"temperature":18,"feelsLike":18,...}`. It travels back through the proxy to the browser.

### 9. Browser: back through the layers

- `HttpWeatherApi.get` emits a `Weather` (`models/weather.ts`, same field names). The store's `map` builds the new state, and the signals update.
- `weather-page.component.html` re-renders: `app-status-message` hides "Loading...", and `@if (store.weather(); as weather)` renders `<app-weather-details [weather]="weather" />`.
- `ui/weather-details/weather-details.component.html` shows temperature, feels like, humidity, wind and clouds from the `weather()` input.

## C. Failure paths

| Where it fails | What happens | What the user sees |
|---|---|---|
| Bad coordinates | Endpoint returns 400 | "Could not load weather" |
| More than 20 requests/min from one IP | Rate limiter returns 429 | same message |
| Provider slow, 5xx or network error | Retry up to 3 times, then `HttpRequestException` or `TimeoutRejectedException` → 502 | same message |
| Many failures in a row | Breaker opens, `BrokenCircuitException` → 503 immediately for 15s | same message |
| API Ninjas answers 400 (its own rate limit) | Not retried, not counted by the breaker, `HttpRequestException` → 502 | same message |
| API container down (Docker) | nginx returns 502 | same message |

In every failure case the store's `catchError` turns the error into the `error` state, and `status-message` shows it.

## D. Dependency picture

```
location-form ──event──▶ weather-page ──▶ WeatherStore ──▶ WeatherApi (abstract)
weather-details ◀─input─┘      (state signals)                  │
status-message  ◀─input─┘                                        ▼
                                                          HttpWeatherApi
                                                                 │ GET /api/weather
───────────────────── browser ▲ ────────────────────────────────▼───────────
              nginx (Docker) or ng serve proxy (development)
                                                                 │ GET /weather
Program.cs: forwarded headers → rate limiter → /weather → IWeatherService (interface)
                                                    │
                                          ApiNinjasWeatherService
                                                    │ resilience pipeline
                                                    ▼
                                             api.api-ninjas.com
```

Both sides follow the same shape: the caller depends on an abstraction (`WeatherApi` on the frontend, `IWeatherService` on the backend), and a single place chooses the implementation (`app.config.ts` and `Program.cs`).

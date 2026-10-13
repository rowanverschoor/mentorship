# Changes since the SOLID rewrite

Goal (from mentor feedback): loose coupling, UI separated from logic, and three patterns: **reactive**, **retry / rate limiting**, **circuit breaker**.


## 1. Frontend: old layout -> new layout

### Before

```
app/
  app.component.ts        template, state, HTTP call logic, everything in one file
  app.config.ts
  config.ts
  models/weather.ts
  services/weather.service.ts   concrete HTTP service, injected directly by the component
```

One component did four jobs: input form, display, state, and calling the service. It depended directly on the concrete `WeatherService`.

### After

```
app/
  app.component.{ts,html,css}            thin shell, only hosts the page
  app.config.ts                          binds WeatherApi -> HttpWeatherApi
  core/
    weather-api.ts                       NEW  abstract contract (WeatherApi)
    http-weather-api.ts                  was services/weather.service.ts
    config.ts                            was app/config.ts
  models/weather.ts                      unchanged
  state/
    weather.store.ts                     NEW  reactive state (signals + RxJS)
  pages/
    weather-page/                        NEW  container, wires store -> UI
  ui/
    location-form/                       NEW  dumb: inputs + button, emits event
    weather-details/                     NEW  dumb: shows the weather
    status-message/                      NEW  dumb: loading / error text
```

### Where each old piece went

| Old | New | What changed |
|---|---|---|
| `app.component.ts` inline template: inputs + button | `ui/location-form/` | Separate `.ts/.html/.css`. Emits `submitted` via `output()`. No services. |
| `app.component.ts` inline template: the `<ul>` of values | `ui/weather-details/` | Separate `.ts/.html/.css`. Takes `weather` via `input()`. No services. |
| `app.component.ts` error `<p>` | `ui/status-message/` | Also shows "Loading..." (new). Inputs only. |
| `app.component.ts` fields `weather`, `error`, `load()` | `state/weather.store.ts` | State and loading logic moved out of the component. |
| `app.component.ts` (rest) | `pages/weather-page/` | Container: injects the store and passes signals to the UI. |
| `services/weather.service.ts` | `core/http-weather-api.ts` | Now `extends WeatherApi`. No longer `providedIn: 'root'`. |
| `config.ts` | `core/config.ts` | Moved only. |
| (none) | `core/weather-api.ts` | New abstract class: the contract the app depends on. |
| `app.config.ts` | `app.config.ts` | Added `{ provide: WeatherApi, useClass: HttpWeatherApi }`. |

### Principles this shows

- **Single responsibility**: form only collects, details only displays, store only holds state, `HttpWeatherApi` only does HTTP, page only wires.
- **Dependency inversion**: the store injects the abstract `WeatherApi`, never `HttpWeatherApi`. The one place that picks the implementation is `app.config.ts`.
- **Open/closed + Liskov**: a mock or cached `WeatherApi` can be added without editing any component.
- **Loose coupling**: `ui/` components talk only through `input()` / `output()`. Nothing in `ui/` imports from `core/` or `state/` (checked with grep).
- **Dependency direction**: `ui <- pages -> state -> core/WeatherApi -> HttpWeatherApi`.

### Reactive pattern (`state/weather.store.ts`)

Before: `load()` called `subscribe()` and set fields by hand.

After:
- Requests go into a `Subject` (`requests$`).
- `switchMap` calls the API. A new request cancels the one in flight, so a slow old answer cannot overwrite a newer one.
- `map` -> success state, `catchError` -> error state, `startWith` -> loading state.
- `toSignal` turns the stream into one state signal.
- `weather`, `loading`, `error` are `computed` signals derived from it. Nothing sets state by hand.
- Any other component can inject the store and read `store.weather()` without another HTTP call (observer pattern: they subscribe to the state).

---

## 2. Backend: new resilience layer

Before `Program.cs` called API Ninjas directly. One bad response threw an unhandled exception (HTTP 500). That is where the earlier `400 Bad Request` stack trace came from: API Ninjas answers 400 for out-of-range coordinates and also for rate limiting.

### Added

```
backend/WeatherApi/
  Resilience/
    ResilienceExtensions.cs    NEW  retry + circuit breaker + timeouts
    RateLimitExtensions.cs     NEW  per-IP rate limiter
  Program.cs                   changed: uses both, validation, error mapping
  WeatherApi.csproj            + Microsoft.Extensions.Http.Resilience 8.10.0
```

### Retry (limited)

`MaxRetryAttempts = 3`, exponential backoff (0.5s, 1s, 2s) with jitter. Retries only temporary failures: network errors, timeouts, 5xx, 408, 429. A 400 is never retried.

### Circuit breaker

If 50% of calls fail (at least 5 calls within 30s), the circuit opens for 15s. While open, calls fail immediately without touching API Ninjas. After 15s one test call decides if it closes again.

### Timeouts

5s per attempt, 20s for the whole operation including retries.

### Pipeline order (outermost first)

```
total timeout -> retry -> circuit breaker -> per-attempt timeout -> HTTP call
```

### Rate limiter (protects our API)

Fixed window per client IP: 20 requests per minute, then HTTP 429. Applied to `/weather` with `.RequireRateLimiting(...)`.

### Validation and error mapping in `Program.cs`

| Situation | Response |
|---|---|
| lat outside -90..90 or lon outside -180..180 | 400 with a message |
| Circuit open (`BrokenCircuitException`) | 503 |
| Retries used up (`HttpRequestException` / `TimeoutRejectedException`) | 502 |
| Too many requests from one IP | 429 |

Before, all of these were an unhandled 500.


## 3. Known limitations

- API Ninjas reports its own rate limit as HTTP 400, so the pipeline does not retry it and it does not trip the breaker.
- The rate limiter counts requests that fail validation too, because it runs before the endpoint.
- Limits (20/min, 3 retries, 15s break) are hardcoded, not in `appsettings.json`.
- The frontend shows one generic "Could not load weather" for every failure, including 429 and 503.
- Nothing is enforced at compile time that `ui/` may not import from `state/` or `core/` (only checked by grep).

## 4. Mentor ideas not implemented

Message queue + worker, Docker / docker compose, load balancer, API gateway, correlation IDs / OpenTelemetry, caching.

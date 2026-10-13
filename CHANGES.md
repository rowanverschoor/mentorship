# Changes since the SOLID rewrite

- **Part 1**: the refactor for the mentor feedback (loose coupling, UI separated from logic, reactive, retry / rate limiting, circuit breaker).
- **Part 2**: everything after that (dark mode, Docker with nginx replacing CORS, cleanup, review fixes).

---

# Part 1: Refactor for the mentor feedback

Goal: loose coupling, UI separated from logic, and three patterns: **reactive**, **retry / rate limiting**, **circuit breaker**.


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

Message queue + worker, load balancer, API gateway, correlation IDs / OpenTelemetry, caching.
(Docker / docker compose was done afterwards, see Part 2.)

---

# Part 2: Changes after the refactor

Everything below happened after Part 1. Some of it **replaces** things Part 1 describes; those are marked.

## 2.1 Dark mode

`src/styles.css` now holds the colors as CSS variables (`--bg`, `--text`, `--error`). A `@media (prefers-color-scheme: dark)` block swaps them, so the app follows the operating system setting. `color-scheme: dark` also darkens the browser's own inputs and button. `status-message.component.css` uses `var(--error)` instead of `crimson`.

`app.component.css` got the page shell styles on `:host` (max width, centered, font).

No manual toggle button exists.

## 2.2 Docker, with nginx replacing CORS

### New files

| File | Purpose |
|---|---|
| `docker-compose.yml` (root) | Starts `api` and `frontend`. Only the frontend publishes a port (4200). The key is required: `${API_NINJAS_KEY:?...}`. |
| `.env` (root, gitignored) | Holds `API_NINJAS_KEY` for compose. Added `.env` to `.gitignore`. |
| `backend/Dockerfile`, `backend/.dockerignore` | Multi-stage .NET 8 build, runs on port 8080 in the container. |
| `frontend/Dockerfile`, `frontend/.dockerignore` | Builds Angular with Node 22, serves the result with nginx. |
| `frontend/nginx.conf` | Serves the Angular files and forwards `/api/` to `http://api:8080/`. |
| `frontend/proxy.conf.json` | Same `/api` forwarding for `ng serve` (development). |

### Files changed

| File | Change |
|---|---|
| `frontend/src/app/core/config.ts` | `API_URL` was `'http://localhost:5149'`, now `'/api'`. (Part 1 said this file was "moved only".) |
| `frontend/package.json` | `start` is now `ng serve --proxy-config proxy.conf.json`. |
| `backend/WeatherApi/Program.cs` | Removed `AddCors` / `UseCors`. Added forwarded-headers handling (below). |
| `backend/WeatherApi/appsettings.json`, `appsettings.Development.json` | Removed the `Cors:AllowedOrigin` setting. |

### How nginx replaces CORS

Before: the page came from `:4200` and called the API on `:5149`. That is a different origin, so the browser blocked it unless the backend allowed it (CORS).

Now: the browser only talks to `:4200`. Whatever serves that port (nginx in Docker, `ng serve` in development) forwards `/api/...` to the backend and strips the `/api` prefix. To the browser it is the same origin, so the backend needs no CORS configuration at all.

```
Before: browser ──▶ :4200 (page)      browser ──▶ :5149 (API)   needs CORS
Now:    browser ──▶ :4200 (page)      browser ──▶ :4200/api ──▶ proxy ──▶ API
```

### Forwarded headers (side effect of the proxy)

Behind a proxy every request comes from the proxy's IP, so the per-IP rate limiter would treat all users as one. nginx therefore sets `X-Forwarded-For` to the real client, and the API trusts it (`UseForwardedHeaders`, in `Program.cs`). This trusts any sender, which is only safe because in Docker the API has **no published port**, so only nginx can reach it.

## 2.3 Cleanup of unused files

| Removed | Why |
|---|---|
| `frontend/README.md` | Generic text generated by Angular CLI; the root `README.md` is the real one. |
| `frontend/tsconfig.spec.json` | There are no tests. |
| `test` target in `angular.json`, `test` script in `package.json` | Same. |
| Jasmine and Karma packages (`@types/jasmine`, `jasmine-core`, `karma`, `karma-chrome-launcher`, `karma-coverage`, `karma-jasmine`, `karma-jasmine-html-reporter`) | Only used by `ng test`. `package-lock.json` changed with them. |
| `frontend/tsconfig.app.json` | Merged into `tsconfig.json` (its `types: []`, `files` and `include`). `angular.json` now builds with `tsconfig.json`. |

If tests are wanted later, re-add them with `ng add` or by hand; `WeatherStore` can be tested with a fake `WeatherApi`.

## 2.4 Fixes found in the full review

| Problem | Fix |
|---|---|
| `appsettings.Development.json` still had the unused CORS setting | Removed. |
| `Location` was defined twice (form and store) | One shared `models/location.ts`, imported by both. |
| Total timeout was 20s, but the worst case is about 24s (4 attempts x 5s + waits of 0.5s, 1s, 2s plus jitter) | Raised to 30s in `ResilienceExtensions.cs`. Part 1's "20s" is therefore outdated. |
| `docker compose up` without a key started with an empty key | Compose now fails with `Set API_NINJAS_KEY in .env`. |
| Page title was "Frontend" | Now "Weather". |
| `angular.json` generator defaults were `inlineTemplate` / `inlineStyle: true`, so `ng generate component` made single-file components | Both set to `false`: new components get separate `.ts`, `.html` and `.css` files. |
| `README.md` and `FLOW.md` described the old structure (`WeatherService`, CORS, `config.ts`) | Rewritten for the current code. |

## 2.5 Structure now

```
mentor/
  README.md  FLOW.md  CHANGES.md  run.sh  docker-compose.yml  .env (ignored)
  backend/
    Dockerfile  .dockerignore
    WeatherApi/  Program.cs  Models/  Services/  Resilience/
  frontend/
    Dockerfile  .dockerignore  nginx.conf  proxy.conf.json
    angular.json  package.json  tsconfig.json
    src/  index.html  main.ts  styles.css
      app/  app.component.*  app.config.ts
            core/  models/  state/  pages/  ui/
```

## 2.6 What was verified

| Check | Result |
|---|---|
| `dotnet build` | passes, 0 warnings |
| `ng build` | passes |
| Development setup (`dotnet run` + `npm start`): page on :4200, `/api/weather` through the `ng serve` proxy | 200 with weather JSON; bad coordinates 400 |
| `docker compose build`, both images | passes |
| Docker stack: `/api/weather` through nginx | 200 with weather JSON |
| Rate limit through nginx | requests beyond 20 per minute get 429 |
| Compose without a key | fails with a clear error |
| Retry behaviour | **not tested** (needs a failing provider) |
| Circuit breaker opening | **not tested** (needs a failing provider) |
| The UI in a browser, including dark mode | **not looked at** |
| Whether nginx forwards the real client IP to the limiter (only seen as: 20 allowed, then 429, from one client) | **not tested with two different clients** |

## 2.7 Known limitations (still true)

- API Ninjas reports its own rate limit as HTTP 400, so the pipeline does not retry it and it does not trip the breaker.
- The rate limiter counts requests that fail validation too, because it runs before the endpoint.
- Limits (20/min, 3 retries, 15s break, timeouts) are hardcoded, not in `appsettings.json`.
- The frontend shows one generic "Could not load weather" for every failure, including 429 and 503.
- Nothing enforces at compile time that `ui/` may not import from `state/` or `core/` (only checked by grep).
- `UseForwardedHeaders` trusts any sender. If the API port were ever published directly, clients could fake their IP and get around the rate limiter. In development (`run.sh`) the API port is open on localhost.
- Docker runs the API in Production mode (no developer exception page), and has no hot reload; keep using `run.sh` for development.

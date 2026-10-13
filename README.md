# Weather App

Enter a latitude and longitude, click a button, see the weather.
Angular frontend + C# (.NET 8) backend, which calls the API Ninjas weather API.

More documentation:
- [`FLOW.md`](FLOW.md): the program flow, function by function
- [`CHANGES.md`](CHANGES.md): what changed and why (for explaining the project)

## Run it

You need an API Ninjas key. Where it goes depends on how you run the app.

### Option 1: Docker (everything in containers)

```bash
echo "API_NINJAS_KEY=<your key>" > .env      # once; .env is gitignored
docker compose up --build
```

Open http://localhost:4200. Stop with `docker compose down`.
Compose refuses to start if the key is missing.

### Option 2: development (hot reload)

```bash
cd backend/WeatherApi
dotnet user-secrets set "ApiNinjas:Key" "<your key>"   # once
cd ../../frontend && npm install                        # once
cd .. && ./run.sh                                       # starts both
```

Open http://localhost:4200. Backend directly: http://localhost:5149/weather?lat=52.37&lon=4.89

Both options use port 4200, so run only one at a time.

## Architecture

```
Browser ──▶ :4200  Angular app  (nginx in Docker, ng serve in development)
              │  /api/weather?lat&lon
              ▼  forwarded by nginx / by the ng serve proxy (proxy.conf.json)
            C# API  (:8080 in Docker, :5149 in development)
              │  retry, circuit breaker, timeouts
              ▼
            api.api-ninjas.com
```

The browser only talks to one origin (`:4200`) and the proxy forwards `/api` to the backend.
That is why the backend needs **no CORS** setup.

## Backend (`backend/WeatherApi`)

| File | Job |
|---|---|
| `Program.cs` | Wiring: registers services, rate limiter, forwarded headers; defines `GET /weather` with validation and error mapping |
| `Models/WeatherResult.cs` | Our own response shape (not API Ninjas') |
| `Services/IWeatherService.cs` | Contract: "get weather for a location" |
| `Services/ApiNinjasWeatherService.cs` | The one class that knows about API Ninjas |
| `Resilience/ResilienceExtensions.cs` | Retry, circuit breaker and timeouts around the API Ninjas calls |
| `Resilience/RateLimitExtensions.cs` | Per-IP rate limit on our own endpoint |
| `Dockerfile` (in `backend/`) | Builds and runs the API in a container |

The API key lives in user-secrets (development) or the `ApiNinjas__Key` environment variable (Docker). It is never in the code or in git.

### What protects the backend

| Mechanism | Setting | Protects against |
|---|---|---|
| Input validation | lat -90..90, lon -180..180 | wasted calls and 400s from the provider |
| Rate limiter | 20 requests/min per client IP, then 429 | one caller using up our quota |
| Retry | 3 retries, exponential backoff + jitter | short hiccups at the provider |
| Circuit breaker | opens at 50% failures (min. 5 calls / 30s), for 15s | hammering a provider that is down |
| Timeouts | 5s per attempt, 30s in total | requests hanging forever |

What the client gets: 400 bad coordinates, 429 too many requests, 502 provider failed after retries, 503 circuit open.

## Frontend (`frontend/src/app`)

Layers (dependencies point one way: `ui ← pages → state → core`):

| Folder | Job |
|---|---|
| `ui/` | Dumb components (`location-form`, `weather-details`, `status-message`). Only `input()` / `output()`, no services. |
| `pages/weather-page/` | Container: connects the store to the UI components. No logic of its own. |
| `state/weather.store.ts` | Reactive state: a request stream (RxJS `switchMap`) exposed as signals (`weather`, `loading`, `error`). |
| `core/weather-api.ts` | Abstract `WeatherApi`: the contract the app depends on. |
| `core/http-weather-api.ts` | `HttpWeatherApi`: the only code that does HTTP. |
| `core/config.ts` | `API_URL = '/api'`. |
| `models/` | `Weather` (shape of the backend JSON) and `Location` (user input). |
| `app.config.ts` | The one place that decides `WeatherApi` → `HttpWeatherApi`. |
| `app.component.*` | Thin shell that hosts the page. |

Other frontend files: `src/styles.css` (global colors, light/dark mode), `proxy.conf.json` (dev proxy), `nginx.conf` (Docker proxy and static hosting), `Dockerfile`.

Every component has its own `.ts`, `.html` and `.css` file.

### Dark mode

`src/styles.css` defines the colors as CSS variables and switches them with `prefers-color-scheme: dark`, so it follows the operating system setting. Components should use `var(--...)` instead of fixed colors.

## SOLID: where it shows up

- **S**: each class has one job. Backend: endpoint, provider, resilience, rate limit. Frontend: form, details, status, page, store, HTTP.
- **O**: add a new weather provider by writing a new `IWeatherService` class (backend) or a new `WeatherApi` class (frontend). Nothing else changes.
- **L**: any `IWeatherService` / `WeatherApi` implementation can replace another.
- **I**: both contracts have one small method.
- **D**: the endpoint depends on `IWeatherService`, the store on `WeatherApi`, never on the concrete classes. Only `Program.cs` and `app.config.ts` name them.

## How Angular starts

`main.ts` → `bootstrapApplication(AppComponent, appConfig)` → the component with selector `app-root` replaces `<app-root>` in `index.html`.

## Angular concepts used

- **Component**: class + template + styles. `imports: [...]` lists what its template needs.
- **`input()` / `output()`**: how a component receives data and reports events. All that connects the UI components to the rest.
- **Dependency injection**: `inject(WeatherApi)` asks Angular for an instance. `{ provide: WeatherApi, useClass: HttpWeatherApi }` says which one to give.
- **Observable (RxJS)**: `http.get()` does nothing until subscribed. `switchMap` cancels the previous request when a new one starts.
- **Signals**: values that tell the template when they change. `toSignal` turns an observable into one; `computed` derives values from other signals.
- **Data binding**: `[(ngModel)]` (two-way), `{{ }}` (class → screen), `(event)` (screen → class), `[property]` (class → child input).
- **`@if`**: shows a block only when its condition is true.

## Things worth being able to explain

- **Why no CORS**: the browser blocks a page on `:4200` from reading answers from another origin such as `:5149` unless the server allows it. The proxy makes everything come from `:4200`, so the problem does not exist. curl never needed CORS because it is the browser that enforces it.
- **Retry vs circuit breaker vs rate limiter**: retry handles a short failure, the breaker stops calling a provider that keeps failing, the rate limiter stops callers from overusing our API.
- **Why the reactive store**: state comes from a stream, so there is one place where it changes, and stale answers are cancelled by `switchMap`.
- **`Task` / `async` / `await`**: the C# way of waiting for slow work (network) without blocking a thread.
- **Why our own `WeatherResult`**: the frontend does not break if the upstream API changes or is swapped.
- **Why the `I` in `IWeatherService`**: .NET naming convention for interfaces (unrelated to the I in SOLID).

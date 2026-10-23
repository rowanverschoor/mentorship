# Changes: queue, worker and SignalR

The weather request is now handled in the background. `POST /weather` puts a job on a queue and answers `202 Accepted` with a job id straight away. A background worker calls API Ninjas, and the result is pushed to the browser over SignalR (a WebSocket). The browser no longer waits on an open HTTP request while retries run.

```
POST /weather ─▶ IWeatherQueue ─▶ WeatherWorker ─▶ IWeatherService (API Ninjas, with retry / circuit breaker)
  ◀ 202 {id}                            │
                                        └─▶ IWeatherNotifier ─▶ WeatherHub ─▶ browser (push with the same id)
```

Built the same way as the rest of the project: each concern has a small contract, one implementation, and one place that wires them together.

---

## Backend (`backend/WeatherApi/`)

| File | Responsibility |
|---|---|
| `Queue/WeatherQueue.cs` | `WeatherJob` record and the `IWeatherQueue` contract. `ChannelWeatherQueue` is an in-memory `Channel` bounded to 100 jobs; when it is full, the endpoint answers 503 instead of letting memory grow. |
| `Queue/WeatherWorker.cs` | `BackgroundService` that reads jobs one at a time, calls `IWeatherService` (a fresh one per job, through a scope), and hands the result or error to `IWeatherNotifier`. Catches every exception, because one that escapes would stop the whole app. |
| `Realtime/WeatherHub.cs` | The SignalR hub browsers connect to. `[Authorize]`, no methods: only the server sends. |
| `Realtime/WeatherNotifier.cs` | `WeatherUpdate` record (id, weather or error), the `IWeatherNotifier` contract, and `SignalRWeatherNotifier`, which pushes to all connections of that user only (`Clients.User`). |

Other backend changes:

- `Program.cs`: `/weather` is now `POST` and returns `202 {id}` (or 503 when the queue is full). Error mapping (502/503) moved from the endpoint to the worker. Registers SignalR, the notifier, the queue and the worker, and maps the hub at `/hubs/weather`.
- `Auth/TokenService.cs`: the token also carries a `NameIdentifier` claim. SignalR uses that claim to find a user's connections; without it, pushes go nowhere.
- `Auth/AuthExtensions.cs`: accepts the token from `?access_token=` on `/hubs` only. Browsers cannot send an `Authorization` header on a WebSocket, so the SignalR client puts the token there.

No new NuGet packages: SignalR is part of ASP.NET Core.

## Proxies

- `frontend/nginx.conf`: passes WebSocket upgrades through on `/api/` (`proxy_http_version 1.1` plus the `Upgrade` and `Connection` headers).
- `frontend/proxy.conf.json`: `"ws": true`, the same for `ng serve`.

## Frontend (`frontend/src/app/`)

| File | Responsibility |
|---|---|
| `core/weather-hub.ts` | `WeatherHub`: one SignalR connection for the app. Sends the current token, reconnects automatically, closes on logout (a connection belongs to one user). Exposes pushes as `updates$`, a `ReplaySubject`, because a push can arrive before the POST's own response. |
| `core/http-weather-api.ts` | `get()` now connects, POSTs, then waits for the push with the matching id. Times out after 60s so the page never shows "Loading..." forever. |
| `package.json` | Added `@microsoft/signalr`. |

`HttpWeatherApi.get()` still returns `Observable<Weather>`, so `WeatherApi`, `WeatherStore`, the page and all UI components are **unchanged**.

## SOLID check

- **S**: queue, worker, delivery, hub and the HTTP endpoint are separate. On the frontend, the connection (`WeatherHub`) and the request flow (`HttpWeatherApi`) are separate.
- **O**: RabbitMQ means a new `IWeatherQueue` implementation. Email or another push channel means a new `IWeatherNotifier`. The endpoint and worker do not change.
- **L**: any `IWeatherQueue` / `IWeatherNotifier` implementation can replace the current one.
- **I**: the contracts are small: two methods on the queue, one on the notifier.
- **D**: the endpoint depends on `IWeatherQueue`; the worker on `IWeatherQueue`, `IWeatherService` and `IWeatherNotifier`, never on `Channel` or SignalR. On the frontend, the store still depends only on `WeatherApi`.

## Verified

- `dotnet build` and `ng build` are clean.
- Full Docker stack through nginx, with the real API key: connecting to the hub without a token returns 401; with a token it connects over WebSocket; `POST /weather` returns 202 with an id; a push with that id and real weather data arrives.
- The push arrived as fast as the 202 itself, which confirms that replaying recent pushes in the frontend is needed.
- Not tested: the UI in a browser, and behaviour while the API restarts or the connection drops.

## Known limits

- Everything is in memory on one API instance. Queued jobs are lost on restart, and several instances would need a real broker (RabbitMQ) and a SignalR backplane (Redis).
- The worker handles one job at a time, so a slow provider call (up to about 30s with retries) holds up the jobs behind it.
- If the user has no open connection when the job finishes, the push is lost. The frontend times out after 60s and shows the normal error.
- The token in the hub URL can end up in nginx access logs. That is the standard SignalR trade-off, limited to `/hubs`.
- A hub connection stays open after its token expires (SignalR checks the token only when connecting). The next POST gets a 401, the interceptor logs out, and logout closes the connection.
- `README.md` and `FLOW.md` still describe the old `GET /weather` flow.

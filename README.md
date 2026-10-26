# Weather App

Log in, enter a latitude and longitude, see the weather.
Angular 22 frontend and C# (.NET 8) backend, which calls the API Ninjas weather API.

More docs:
- [`FLOW.md`](FLOW.md): the program flow, function by function
- [`CHANGES.md`](CHANGES.md), [`changes_jwt.md`](changes_jwt.md), [`changes_queue_signalr.md`](changes_queue_signalr.md): what changed and why

## Run it

First run `./setup-secrets.sh` once. It asks for your API Ninjas key, generates a JWT key, and writes `.env` and the dotnet user-secrets. The login is `rowan` / `test123`.

**Docker:**

```bash
docker compose up --build
```

**Development (hot reload):**

```bash
cd frontend && npm install && cd ..   # once
./run.sh                              # starts backend and frontend
```

Both open on http://localhost:4200, so run one at a time.

## How it works

```
Browser ──▶ :4200 Angular (nginx in Docker, ng serve in dev)
              │  /api/*  forwarded by the proxy, so no CORS
              ▼
            C# API
              POST /login     ─▶ JWT (1 hour)
              POST /weather   ─▶ queue ─▶ 202 {id}
                                   │
                              WeatherWorker ─▶ API Ninjas (retry, circuit breaker, timeouts)
                                   │
              /hubs/weather   ◀────┘ result pushed over SignalR with the same id
```

1. **Login.** `POST /login` returns a JWT. The frontend keeps it in `sessionStorage` and an interceptor sends it as a bearer token. A 401 logs you out.
2. **Request.** `POST /weather` puts a job on an in-memory queue (max 100) and answers `202` with a job id right away.
3. **Worker.** A background worker takes jobs one by one and calls API Ninjas.
4. **Push.** The result goes back to that user's browser over SignalR. The frontend matches it to the job id and gives up after 60 seconds.

## Protection

| What | Setting |
|---|---|
| Input validation | lat -90..90, lon -180..180 |
| Rate limit | 20 requests/min per IP, 5 login attempts/min per IP |
| Retry | 3 retries, exponential backoff with jitter |
| Circuit breaker | opens at 50% failures, for 15s |
| Timeouts | 5s per attempt, 30s total |
| Queue full | 503 |

## Structure

Every part has a small contract, one implementation, and one place that wires them together (`Program.cs` in the backend, `app.config.ts` in the frontend). Swapping something, like RabbitMQ for the queue, means writing one new class.

**Backend (`backend/WeatherApi`):** `Auth/` (JWT and login), `Queue/` (queue and worker), `Realtime/` (SignalR hub and notifier), `Resilience/` (retry, breaker, rate limits), `Services/` (API Ninjas client).

**Frontend (`frontend/src/app`):** `ui/` (dumb components), `pages/` (connects store to UI), `state/` (reactive store), `core/` (HTTP, auth, SignalR hub).

## Known limits

- One hardcoded user, no password hashing.
- Queue and pushes are in memory on one instance. Jobs are lost on restart.
- The worker handles one job at a time.

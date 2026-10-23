# Changes: JWT login

The weather endpoint now requires a login. `POST /login` swaps a username and password for a JWT (valid 1 hour). The frontend keeps the token and sends it as `Authorization: Bearer <token>` on every request.

Built the same way as the rest of the project: each concern has a small contract (interface / abstract class), one implementation, and one place that wires them together.

---

## Backend (`backend/WeatherApi/Auth/`)

| File | Responsibility |
|---|---|
| `AuthOptions.cs` | Typed config: `JwtOptions` (Issuer, Audience, Key; validated at startup, so a missing or short key stops the app instead of failing on the first request) and `AuthOptions` (the single login user). |
| `TokenService.cs` | `ITokenService` contract and its `JwtTokenService` implementation for issuing a token. |
| `UserValidator.cs` | `IUserValidator` contract and its `ConfiguredUserValidator` implementation for checking credentials. Uses the single user from config (`Auth:User`, `Auth:Password`) with a fixed-time comparison. Swap for a database or identity provider without touching the endpoint. |
| `AuthExtensions.cs` | Wiring only: `AddJwtAuth()` registers options, services and bearer validation (signature, issuer, audience, expiry); `MapLogin()` is the `/login` endpoint. Also holds the small `LoginRequest` record. |

Other backend changes:

- `Program.cs`: `AddJwtAuth(...)`, `UseAuthentication()`, `UseAuthorization()`, `MapLogin()`. `/weather` now has `.RequireAuthorization()`.
- `WeatherApi.csproj`: added `Microsoft.AspNetCore.Authentication.JwtBearer`.
- `appsettings.json`: `Jwt:Issuer` and `Jwt:Audience`. The key and the login are secrets and are not stored here.

- `Resilience/RateLimitExtensions.cs`: second policy `login`, 5 attempts per minute per client IP, then HTTP 429. Applied to `/login` so the password cannot be brute-forced.

## Frontend (`frontend/src/app/`)

Mirrors the `WeatherApi` / `HttpWeatherApi` pattern.

| File | Responsibility |
|---|---|
| `core/auth-api.ts` | Abstract contract: `login(username, password)` returns the token. |
| `core/http-auth-api.ts` | Calls `POST /api/login`. |
| `core/auth.ts` | Session state (`token`, `loggedIn` signals, `login`, `logout`). Keeps the token in `sessionStorage` so it survives a reload. Depends on `AuthApi` only. |
| `core/auth.interceptor.ts` | Adds the bearer header. A 401 on an authenticated request logs the user out (expired or invalid token). |
| `ui/login-form/` | Dumb-ish login form; calls `Auth.login`, shows "Wrong username or password", or "Too many attempts, try again in a minute" on a 429. |
| `app.component.*` | Shows the login form, or the weather page plus a Log out button, depending on `auth.loggedIn()`. |
| `app.config.ts` | `AuthApi -> HttpAuthApi` binding and the interceptor, next to the existing `WeatherApi` binding. |

## Config and secrets

- `docker-compose.yml`: passes `Jwt__Key`, `Auth__User`, `Auth__Password` from `.env` (compose refuses to start if one is missing). Frontend port changed from `4200` to `1337`.
- `setup-secrets.sh`: writes `.env` and sets `dotnet user-secrets` for local runs. It generates a random JWT key. The login is hardcoded to `rowan` / `test123`, so change it before any real deploy.

## SOLID check

- **S**: token creation, credential checking, config, wiring and the endpoint are separate. On the frontend, HTTP, state and interceptor are separate.
- **O**: a new user store or token format means a new implementation, not edits to the endpoint or `Auth`.
- **L**: any `IUserValidator` / `ITokenService` / `AuthApi` implementation can replace the current one.
- **D**: the endpoint, `Auth`, the form and the interceptor depend on contracts or on `Auth`, never on HTTP or JWT details.

## Verified

- `dotnet build` and `ng build` are clean.
- Against the running API: no token returns 401, wrong password returns 401, the 6th login attempt within a minute returns 429, valid token passes auth (the provider call then returns 502, since only a fake provider key was used).
- Not tested: the UI in a browser, and the Docker setup.

## Known limits

- One configured user; no registration and no password hashing (the password comes from config).
- The token lasts 1 hour and there is no refresh. When it expires, a 401 returns the user to the login form.
- The token is in `sessionStorage`, so it is readable by any script on the page. That is acceptable here, but it is the trade-off against httpOnly cookies.

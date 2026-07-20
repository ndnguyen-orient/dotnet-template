# CLAUDE.md

Guidance for Claude Code working in this repo. Keep current as the project grows.

## What this is

A **Clean-Architecture .NET service** template, packaged as a `dotnet new` template
(`.template.config/template.json`, `sourceName: Sample`). Vertical-slice CQRS over a
**lightweight in-process mediator** (`Sample.Application/Messaging`, no MediatR), an optional
**React + Vite SPA** (`source/Sample.Api/ClientApp`), a Widgets sample, and `/health`. Ships
with GitHub Actions CI and Claude skills.

> **Persistence:** EF Core + **PostgreSQL** (`Npgsql`) for runtime queries only. `AppDbContext`
> lives in `Sample.Infrastructure/Persistence`; the Widgets slice persists through it via
> `EfWidgetRepository` (port = `IWidgetRepository` in Application). Connection string key
> `ConnectionStrings:AppDb` (appsettings + env). Local Postgres via `docker-compose.yml`.
>
> **Schema migrations:** owned by **EF Core migrations** (`dotnet-ef` in the tool manifest).
> Migration code lives in `source/Sample.Infrastructure/Migrations/`; the design-time context is
> built by `AppDbContextFactory` (override the connection with the `AppDb__ConnectionString` env
> var). Covers the Widgets slice **and ASP.NET Core Identity** tables (auth added Identity, which
> is EF-migration-native). Add a migration with `dotnet ef migrations add <Name> --project
> source/Sample.Infrastructure --startup-project source/Sample.Api`; commit the generated files.
> Apply with `dotnet ef database update` (same project flags). **In Development the API
> auto-migrates and seeds** via `DbInitializer.MigrateAndSeedAsync` (`Program.cs`, guarded to
> `IsDevelopment()`); in production migrations run as a deliberate deploy step (see
> `deploy.yaml`), never on startup. EF column mappings (`WidgetConfiguration`, snake_case) define
> the schema — there is no separate hand-written SQL to keep in sync.

> **Auth:** **JWT, cookie-transported**, **role-based** (RBAC). Design spec: `docs/authentication.md`.
> Both tokens travel **only as `httpOnly` cookies** — never in the response body or readable by JS
> (XSS-safe), with `Secure` (tracks request scheme), `SameSite=Strict` (CSRF-safe, no separate
> token). The **access token** (HS256 JWT, 15 min) is scoped `Path=/`; the **refresh token**
> (opaque, SHA-256-hashed in `refresh_tokens`, **fixed 7-day TTL — not reset on rotation**) is
> scoped `Path=/api/auth/refresh`. Cookie read/write/clear is centralized in `AuthCookies`
> (`Sample.Api/Security`); `AuthCookies` flags + `JwtBearerEvents.OnMessageReceived` (in `Program.cs`,
> pulls the access token from the cookie) are the only cookie-specific wiring. ASP.NET Core Identity
> is the **user store** (`AddIdentityCore<AppUser>` — `UserManager`/`RoleManager`/PBKDF2 hashing),
> `AppUser : IdentityUser` + `AppDbContext : IdentityDbContext<AppUser>`. `AuthEndpoints` exposes
> `POST /api/auth/register`, `POST /api/auth/login` (sets both cookies), `POST /api/auth/refresh`
> (rotates the refresh cookie; reuse → revoke whole chain → 401), and `POST /api/auth/logout`
> (authorized; revokes all the caller's refresh tokens + clears cookies). Identity is separate:
> `GET /api/profile/me` (`ProfileEndpoints`). `TokenService` signs HS256 tokens; `RefreshTokenService`
> (`Sample.Infrastructure/Security`) issues/rotates/revokes. Config = `Jwt` section (`SigningKey`
> dev value in `appsettings.json`; override via `Jwt__SigningKey` secret in prod). `DbInitializer`
> seeds roles (`admin`, `user`) + optional dev admin (`Seed:Admin:*`). Endpoints opt in with
> `.RequireAuthorization()` / `.RequireAuthorization(p => p.RequireRole("admin"))`. Handlers read
> the caller via the `ICurrentUser` port (Application), implemented by `CurrentUser` (Api) over
> `HttpContext.User`. Integration tests bypass auth with a header-driven `TestAuthHandler`
> (`TestWebAppFactory.UseTestAuthentication`); `AuthEndpointsTests` exercises the real cookie/JWT
> pipeline end-to-end.

> **Client framework:** template param `--client-framework` (`-cf`) = `react` (default) or
> `none` (API only). Driven by `ClientFramework` symbol → computed `UseReact` / `UseApiOnly`,
> applied via `#if (UseApiOnly)` regions in `.cs` and `<!--#if-->` in `.csproj`.

> **Observability:** **OpenTelemetry** (traces + metrics + logs) wired in `Program.cs` — ASP.NET
> Core / HttpClient / runtime / EF Core / Npgsql instrumentation, all three signals exported over
> **OTLP** under one shared resource so a backend can stitch a trace to its own logs. The exporter
> reads the standard `OTEL_EXPORTER_OTLP_*` env vars (endpoint default `http://localhost:4317`);
> point it at a collector per environment. Logs also flow through the standard `ILogger` pipeline
> (JSON console in non-Development) with `TraceId`/`SpanId` stamped via `ActivityTrackingOptions`,
> plus an OTLP log exporter with `IncludeFormattedMessage`/`IncludeScopes` so exported records
> carry the rendered message and the trace/span id. `LoggingBehavior` (`Sample.Application/
> Messaging`) logs every CQRS request + elapsed ms. **Local stack:** `docker-compose.yml` runs
> Grafana's all-in-one `otel-lgtm` image (Loki + Grafana + Tempo + Mimir + OTel Collector) —
> Grafana UI at `http://localhost:3000` (`admin`/`admin`), OTLP receivers on 4317 (gRPC) / 4318
> (HTTP). Dashboards live in `observability/dashboards/*.json`, auto-provisioned via
> `observability/provisioning/dashboards/dashboards.yaml` (repo is source of truth,
> `allowUiUpdates: false`) — ships with `Sample.Api — Golden Signals` (RPS/error rate/p95 latency
> by route, active requests, GC pauses, working set, DB pool + p95 query time + query rate).
> **Not for production** (single-node, no auth on OTLP ingest). **Health checks** split:
> `/health/live` (liveness, no deps), `/health/ready` (readiness — DB via `AddDbContextCheck`,
> tag `ready`), `/health` kept as a liveness alias. Probes: liveness→`/health/live`,
> readiness→`/health/ready`.

> **SDK note:** targets **net10.0** (`global.json` pins SDK 10, prerelease allowed). A net10
> SDK must be installed to build/test locally; otherwise every `dotnet` command in this repo
> fails the version check.

## Layout

| Path | Purpose |
|------|---------|
| `source/<Name>/` | Application & library projects (NOT `src/`): Domain / Application / Infrastructure / Api |
| `source/Sample.Api/ClientApp/` | React + Vite SPA (excluded when `--client-framework none`) |
| `tests/<Name>.Tests/` | Test projects (xUnit + AwesomeAssertions) |
| `Sample.sln` | Solution — add every project to it |
| `Directory.Build.props` | Shared MSBuild settings + `PublishDir` → `output/apps/...` |
| `global.json` | Pins .NET SDK 10 (prerelease) |
| `GitVersion.yml` | Versioning (GitVersion 6, `GitHubFlow/v1`; main = ContinuousDeployment); CI derives version from git history |
| `.config/dotnet-tools.json` | Local dotnet tool manifest (`dotnet tool restore`) |
| `docker-compose.yml` | Local dev Postgres (matches default `ConnectionStrings:AppDb`) |
| `Dockerfile` | Multi-stage container build, non-root, `/health` for orchestrator probes |
| `.github/workflows/ci.yaml` | Build + test + coverage (GitVersion-stamped) |
| `.github/workflows/pr-lint.yml` | Conventional-Commit PR-title lint |
| `.template.config/` | `dotnet new` template metadata |
| `.claude/skills/` | Reusable Claude workflows |

## Conventions

- **TFM**: `net10.0`, `LangVersion 14`, `Nullable enable`, `ImplicitUsings enable`.
- **Folders**: app code under `source/`, tests under `tests/`.
- **Tests**: xUnit + AwesomeAssertions. `[Fact]` / `[Theory]`, ctor for setup, method names `Thing_does_x`.
- **Packages**: versions inline on `<PackageReference>` (no central package management here).
- **Versioning**: never hand-edit version numbers — GitVersion computes them in CI from git history.
- **PR titles**: must follow Conventional Commits (enforced by `pr-lint`).

## Commands

```bash
dotnet restore && dotnet tool restore
dotnet build -c Release
dotnet test  -c Release

docker compose up -d                      # start local Postgres (empty; EF creates the schema)
dotnet run --project source/Sample.Api   # serves /health, /api/widgets, /openapi/v1.json
                                          #   (in Development, auto-applies EF migrations + seeds)

# DB schema migrations via EF Core (dotnet-ef is in the tool manifest):
#   add a migration after changing entities/DbContext:
dotnet ef migrations add Describe_change \
  --project source/Sample.Infrastructure --startup-project source/Sample.Api
#   apply pending migrations manually (Development does this automatically on startup):
export AppDb__ConnectionString="Host=localhost;Port=5432;Database=sample;Username=sample;Password=sample"
dotnet ef database update \
  --project source/Sample.Infrastructure --startup-project source/Sample.Api

# Auth (JWT in httpOnly cookies): register, log in (sets access_token + refresh_token cookies into
# a jar), then send the jar on protected calls. The browser does this automatically; curl uses -b/-c.
curl -X POST "http://localhost:5080/api/auth/register" -H "Content-Type: application/json" -d '{"email":"me@x.com","password":"Passw0rd!$"}'
curl -c cookies.txt -X POST "http://localhost:5080/api/auth/login" -H "Content-Type: application/json" -d '{"email":"me@x.com","password":"Passw0rd!$"}'
curl -b cookies.txt http://localhost:5080/api/widgets      # 200; POST /api/widgets needs the "admin" role
curl -b cookies.txt http://localhost:5080/api/profile/me   # 200; current user + roles
curl -b cookies.txt -c cookies.txt -X POST "http://localhost:5080/api/auth/refresh"  # rotates the cookie pair

# React client (separate terminal): http://localhost:5173, proxies /api etc. to the API
cd source/Sample.Api/ClientApp && npm install && npm run dev
```

## Adding a project

```bash
dotnet new webapi  -o source/MyThing      -n MyThing
dotnet sln add source/MyThing/MyThing.csproj
dotnet new xunit   -o tests/MyThing.Tests -n MyThing.Tests
dotnet sln add tests/MyThing.Tests/MyThing.Tests.csproj
dotnet add tests/MyThing.Tests/MyThing.Tests.csproj reference source/MyThing/MyThing.csproj
```
Then set TFM to `net10.0` and `LangVersion 14` in both csproj files.

## Using as a `dotnet new` template

```bash
dotnet new install .                       # install from this folder
dotnet new ai-service -n PaymentsApi                          # React SPA (default)
dotnet new ai-service -n PaymentsApi --client-framework none  # Web API only
```
`Sample` is renamed to the project name.

## CI/CD

- **ci.yaml**: on push/PR to `main` — GitVersion, restore (+tool restore), build, test + coverage, annotate.
- **pr-lint.yml**: validates the PR title (Conventional Commits).
- **deploy.yaml**: skeleton pipeline (`workflow_dispatch`) — GitVersion-tagged image build + push to
  **GHCR**, then a `migrate` job, then a placeholder `deploy` job. The `migrate` job restores tools
  and runs `dotnet ef database update` (connection string from the `DB_CONNECTION_STRING`
  env-scoped secret, passed as `AppDb__ConnectionString`) before rollout. Swap GHCR→ECR/GAR/ACR and
  fill the `deploy` job for your target. Still light CI: nothing deploys on push/PR — only when you
  dispatch it.

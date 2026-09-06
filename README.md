# Findatime API

REST API for scheduling events, built with a feature-sliced layered architecture (Api / Domain / Infra per feature).

## Tech stack

- [.NET 10](https://dotnet.microsoft.com/) (minimal APIs)
- [EF Core](https://learn.microsoft.com/ef/core/) + [Npgsql](https://www.npgsql.org/) for PostgreSQL
- [PostgreSQL 18](https://www.postgresql.org/) (container defined in `docker-compose.yml`, run via Podman)

## Dependencies

- .NET SDK 10
- Podman (`podman-compose` for the container orchestration)

## How to run

1. Start PostgreSQL:

   ```
   podman compose up -d
   ```

   (alternatively `podman-compose up -d`; both read `docker-compose.yml`)

2. Apply database migrations:

   ```
   dotnet ef database update
   ```

3. Run the API:

   ```
   dotnet run --launch-profile http
   ```

   The API listens on `http://localhost:5263`. Create an event:

   ```
   curl -X POST http://localhost:5263/events \
     -H 'Content-Type: application/json' \
     -d '{"name":"Team Sync"}'
   ```

## Database configuration

Secrets are never stored in committed files. Committed `appsettings.json` only contains the harmless local container defaults; every real credential lives in a gitignored `.env` file (local dev) or in the deployment platform's secret store (Render).

### Connection strings

- `ConnectionStrings:LocalConnection` — local PostgreSQL container (`localhost:5433`, database `findatime`). Committed default in `appsettings.json`; overridable via `.env`.
- `ConnectionStrings:StagingConnection` — external staging database (Neon). No committed value; comes from `.env` locally and from a Render env var in deployment.

`Program.cs` picks the key by environment: non-Staging reads `LocalConnection`, `Staging` reads `StagingConnection`.

### Local development

The local container DB works out of the box — no setup:

```
dotnet run --launch-profile http
```

Do **not** keep the `staging` profile first in `Properties/launchSettings.json`; plain `dotnet run` uses the first profile, so `http` must stay first to keep local runs on the local DB.

### Staging locally (against Neon)

1. Copy the template and fill in the real connection string:

   ```
   cp .env.example .env
   # edit .env -> ConnectionStrings__StagingConnection=...
   ```

2. Run with the staging profile (reads the secret from `.env`):

   ```
   dotnet run --launch-profile staging
   ```

Migrations against staging use the same `.env`:

```
dotnet ef database update --environment Staging
```

`.env` is gitignored — never commit it. Migrations are generated with `dotnet ef migrations add <Name>` and stored under `Migrations/`.

### Staging deployment (Render)

Set `ASPNETCORE_ENVIRONMENT=Staging` and `ConnectionStrings__StagingConnection=<real value>` as Render environment variables. No `.env` is deployed; Render env vars feed the built-in configuration provider and take precedence.
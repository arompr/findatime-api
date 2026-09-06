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

## Running the API in a container

The API ships as a standalone container image (no DB inside it — point it at your
Postgres via `ConnectionStrings__Postgres`). Build and run it with:

```
podman build -t findatime-api .
podman run -p 5263:8080 \
  -e ASPNETCORE_ENVIRONMENT=Staging \
  -e ConnectionStrings__Postgres='<connection string>' \
  findatime-api
```

The container listens on `:8080` (exposed as `5263` locally). Health check:
`GET /health`. Configuration is provided via environment variables, never baked
into the image (see [Database configuration](#database-configuration)). The
`docker-compose.yml` Postgres service is only for local development; the
containerized API does not start or manage a database.

## Database configuration

Secrets are never stored in committed files. Committed `appsettings.json` only contains the harmless local container defaults; every real credential lives in a gitignored `.env` file (local dev) or in the deployment platform's secret store (Render).

There is a single connection string key, `ConnectionStrings:Postgres`, in every environment. It is resolved from the config sources in precedence order (committed `appsettings.json` default → `.env` → `.env.{Environment}` → real environment variables).

`Program.cs` loads `.env` and `.env.{EnvironmentName}` convention files via DotNetEnv:

```csharp
builder.Configuration.AddDotNetEnvMulti(
    [".env", $".env.{builder.Environment.EnvironmentName.ToLowerInvariant()}"]);
```

Later files override earlier ones, so `.env.staging` wins over `.env` when running the staging profile (files are lowercase, e.g. `.env.staging`).

### Local development

The local container DB works out of the box — no setup needed, `appsettings.json` has the localhost default:

```
dotnet run --launch-profile http
```

Optional: copy `.env.example` to `.env` to override local settings. Do **not** keep the `staging` profile first in `Properties/launchSettings.json`; plain `dotnet run` uses the first profile, so `http` must stay first to keep local runs on the local DB.

### Staging locally (against Neon)

1. Copy the templates and fill in real values:

   ```
   cp .env.example .env
   cp .env.staging.example .env.staging
   # edit .env.staging -> ConnectionStrings__Postgres=<neon connection string>
   ```

2. Run with the staging profile (loads `.env` then `.env.staging`, so staging wins):

   ```
   dotnet run --launch-profile staging
   ```

Migrations against staging use the same files:

```
dotnet ef database update --environment Staging
```

`.env` and `.env.staging` are gitignored — never commit them. Migrations are generated with `dotnet ef migrations add <Name>` and stored under `Migrations/`.

### Staging deployment (Render)

Set `ASPNETCORE_ENVIRONMENT=Staging` and `ConnectionStrings__Postgres=<real value>` as Render environment variables. No `.env` files are deployed; Render env vars feed the built-in configuration provider. The application reads the same `ConnectionStrings:Postgres` key in every environment.
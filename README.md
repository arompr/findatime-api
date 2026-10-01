# Findatime API

REST API for scheduling events and meetings: create an event, share its link,
and let participants indicate availability.

## Stack

- .NET 10 minimal APIs, EF Core + Npgsql for writes, Dapper over raw SQL for reads
- PostgreSQL 18 (run locally via Podman, `docker-compose.yml`)
- OpenAPI document (Development only)

## Docs

- [Documentation index](Docs/README.md) — features and where each part is
  documented
- [Database migrations](Docs/db-migrations.md) — how to change the schema and
  apply migrations

## API typescript client

TypeScript types for the API are generated from the OpenAPI document and
published to GitHub Packages as `@arompr/findatime-typescript-client` (see
[`typescript-client/`](typescript-client/README.md)). Publishing is done manually via the
`publish-typescript-client` GitHub Actions workflow. To regenerate locally, run
`make typescript-client`.

## Prerequisites

- .NET SDK 10
- Podman (`podman compose`)

## Quick start

```
# 1. Start Postgres (localhost:5433)
podman compose up -d

# 2. Apply migrations
dotnet ef database update

# 3. Run the API
dotnet run
```

API listens on <http://localhost:5263> (OpenAPI at /openapi/v1.json). Verify:
`GET /health`.

## REST scratch files (Hurl)

Each feature's REST surface has a hand-maintained, runnable scratch file under
`src/FindAtime.Api/Features/<Feature>/Api/` (e.g. `events.hurl`,
`availability.hurl`). They use [Hurl](https://hurl.dev) because the availability
read endpoints use the non-standard `QUERY` verb, which common `.http` clients
cannot send.

Install Hurl with your package manager or any method from the
[installation guide](https://hurl.dev/docs/installation.html) — for example
`pacman -Sy hurl` (Arch), `brew install hurl` (macOS), or
`cargo install --locked hurl`.

With the API running, execute a file:

```
hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl
hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl
```

Each entry asserts at least its status code (`HTTP 200`, `HTTP 201`, …). The
files are self-contained and run against `http://localhost:5263`: each creates
its own event (and, for availability, a participant) and threads the returned
ids into later calls with [Hurl captures](https://hurl.dev/docs/capturing-response.html),
so they can be run repeatedly without manual setup.

### Inspect a single request

The files are workflows, but you can run one request without splitting them out
into separate files. Each file's header lists its entries; `--to-entry N` runs
entries 1..N, reuses the captures made by the earlier entries, and prints only
the last response body (pretty-printed when stdout is a terminal):

```
hurl --to-entry 3 src/FindAtime.Api/Features/Events/Api/events.hurl      # GET /events/{publicId}
hurl --to-entry 4 src/FindAtime.Api/Features/Availability/Api/availability.hurl  # QUERY /availabilities
```

`--from-entry N` alone cannot be used for id-dependent entries, because their ids
come from captures in the entries before them.

## Container

The API image contains no DB — point it at Postgres via env vars. Env files are
fed to Podman directly (see `Makefile`):

```
make container-dev        # local: host network, reaches localhost:5433, serves on :8080
make container-staging    # staging-like: :5263 -> :8080, uses .env.staging (remote DB)
```

## Configuration

Secrets live in gitignored `.env` / `.env.staging` (see `.env.example`); the app
loads `.env` then `.env.{Environment}` in that order. Never commit
real credentials.

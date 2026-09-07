# Findatime API

REST API for scheduling events and meetings: create an event, share its link,
and let participants indicate availability.

## Stack

- .NET 10 minimal APIs, EF Core + Npgsql for writes, Dapper over raw SQL for reads
- PostgreSQL 18 (run locally via Podman, `docker-compose.yml`)
- OpenAPI document (Development only)

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

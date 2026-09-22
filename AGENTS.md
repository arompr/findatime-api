# AGENTS.md

## What this application is

**Findatime** is a REST API for scheduling events and meetings. It is the backend
of an event-scheduling product. The product's goal is to let people create
scheduling events, share them, and let participants indicate the times they are
available so an organizer can pick a slot that works for everyone.

Current behavior is documented per feature under `Docs/specs/` — see
`Docs/README.md` for the index of features and where each feature's
requirements, spec, and design live.

## Tech stack

- **.NET 10** (net10.0), **ASP.NET Core minimal APIs** (no controllers, no MVC).
- **C#** with `Nullable` and `ImplicitUsings` enabled. Root namespace `findatime_api`.
- **EF Core 10 + Npgsql** for PostgreSQL persistence (writes) and migrations.
- **Dapper** for read queries, executing raw SQL loaded from embedded resources.
- **PostgreSQL 18** running in a container via Podman (`docker-compose.yml`).
- OpenAPI document auto-generated via `AddOpenApi()`/`MapOpenApi()`.

## Project layout

Feature-sliced, layered-per-feature. Each feature lives under `Features/` and is
split into sublayers. The code uses simple classes (no interfaces yet) wired up
manually in `Program.cs`.

```
src/FindAtime.Api/
  Features/
    Events/
      Api/            HTTP layer (endpoint mapping, request + response DTOs)
      Application/    Use cases (CreateEvent, GetEvent, JoinEvent, ...)
        Exceptions/   Application exceptions (EventNotFound, InvalidPasscode, ...)
      Domain/         Domain model + factory (Event, EventId, EventFactory)
      Infra/          Persistence (EF Core write side)
        Read/         Read side (raw SQL + Dapper)
  Common/
    Sql.cs            Loads embedded SQL resources by filename
    ExceptionHandler.cs  Maps application exceptions to HTTP responses
  Program.cs          Composition root / DI registration
tests/
  FindAtime.UnitTests/        xunit unit tests
  FindAtime.IntegrationTests/ xunit integration tests (Testcontainers.PostgreSql)
```

Layering rules:

- **Api** depends on application services (`CreateEvent`, `GetEvent`).
- **Application** (use-case classes under `Features/<Feature>/Application/`)
  orchestrates `Domain` factories and `Infra` repositories/services; it
  returns/accepts DTOs.
- **Domain** is pure C# with no EF or HTTP dependencies.
- **Infra** maps domain objects to DB models. The **write** side uses EF Core
  (`DbContext`, `EventRepository`). The **read** side uses a single
  `NpgsqlDataSource` + Dapper running SQL files loaded via `Sql.Load(...)`.

## Conventions

- Prefer feature slices over cross-cutting folders; add a feature under
  `Features/<FeatureName>/` with the same `Api/Application/Domain/Infra`
  sub-structure (application exceptions under `Application/Exceptions/`).
- Signal expected failures from use cases by throwing application exceptions
  (e.g. `EventNotFoundException`). `Common/ExceptionHandler` maps exception type
  to an HTTP status and machine-readable `error` code through a single
  `Dictionary<Type, (int, string)>`; response bodies are `{ message, error }`.
  Add new mappings to that dictionary — do not chain `if` statements.
- Request-shape validation (malformed uuid, blank required fields) stays as
  inline `Results.BadRequest` in the endpoints.
- Response objects (`*Response` records) are their own types, live in the
  `Api` folder, and are what endpoints return. Map application DTOs into them;
  do not return application DTOs directly from endpoints.
- Keep the read and write sides separated (CQRS-lite): writes via EF Core,
  reads via raw SQL + Dapper.
- SQL read queries live in `*.sql` files embedded as resources, referenced by
  filename through `Common/Sql.cs`. New SQL files must be added to the
  `<EmbeddedResource>` items in `src/FindAtime.Api/FindAtime.Api.csproj`.
- Manual DI registration in `Program.cs` (no `IServiceCollection` extensions so
  far). Register per use-case/service; use the existing lifetime patterns
  (`Scoped` for repositories/services/use-cases, `Singleton` for stateless
  factories).
- Domain entities use typed ids (e.g. `EventId`) rather than raw GUIDs.
- Use records for request/response DTOs (e.g. `CreateEventRequestParams`,
  `GetEventResponse`).
- Follow the existing naming: private fields are `_camelCase`, PascalCase public
  members, 4-space indentation (see `.editorconfig`). Do not add comments unless
  asked.

## Spec-driven development

Specs live under `Docs/`. A **feature spec** is the current truth about one part
of the product; a **change** is temporary work toward a new state.

```
Docs/
  README.md                 index of features and doc layout
  specs/                    current truth, one folder per feature
    _templates/             templates for every file below
    events/                 requirements.md / spec.md / design.md
    availability/           requirements.md / spec.md / design.md
  changes/                  historical/temporary work, one numbered folder per change
    001-initial-availability/   requirements.md / design.md / tasks.md
```

Roles of the three feature-spec files:

- `requirements.md` = **enduring product intent** — why the feature exists, user
  stories, acceptance criteria. WHAT and WHY, never HOW.
- `spec.md` = **authoritative current behavior** — purpose, invariants, state,
  behavior, and API contract. This is what the system currently guarantees.
- `design.md` = **authoritative current architecture** — aggregate boundaries,
  relationships, API shape, persistence model, validation, integration points,
  and non-obvious decisions. Document what an agent can't infer from one or two
  files; never line-level implementation detail.

A change (`Docs/changes/NNN-name/`) is the vehicle that moves a feature from one
state to the next:

- `requirements.md` = what this change needs to accomplish (scope + acceptance).
- `design.md` = how this change will be implemented (the *transition*).
- `tasks.md` = ordered, dependency-annotated work items with acceptance criteria
  and verification steps.

Workflow: write the change's `requirements.md` → `design.md` → `tasks.md`, then
implement `tasks.md` in order. When a change lands, fold its deltas into the
feature's `requirements.md`/`spec.md`/`design.md` so they remain the current
truth, and leave the change folder untouched as the historical record.

## Database

- PostgreSQL 18 via Podman; local connection string default in
  `appsettings.json` (`ConnectionStrings:Postgres`, default `localhost:5433`,
  db/user/pass `findatime`). The same key is used in every environment.
  Locally it is overridden by gitignored `.env` and `.env.staging` files,
  loaded by `Program.cs` via `AddDotNetEnvMulti([".env",
$".env.{EnvironmentName.ToLowerInvariant()}"])`; `.env.staging` wins over
  `.env`. In
  deployment (Render) the value comes from the `ConnectionStrings__Postgres`
  env var. Never put real credentials in committed files.
- EF Core migrations are stored under `Migrations/` and generated/updated with
  the `dotnet ef` CLI.

## Commands

```bash
# Start PostgreSQL
podman compose up -d          # or: podman-compose up -d

# Apply migrations
dotnet ef database update --project src/FindAtime.Api/FindAtime.Api.csproj

# Run the API (http profile on :5263)
dotnet run --project src/FindAtime.Api/FindAtime.Api.csproj --launch-profile http

# Run the API against the external staging DB (see appsettings.Staging.json)
dotnet run --project src/FindAtime.Api/FindAtime.Api.csproj --launch-profile staging

# Apply migrations to the staging DB
dotnet ef database update --environment Staging --project src/FindAtime.Api/FindAtime.Api.csproj

# Add a migration
dotnet ef migrations add <Name> --project src/FindAtime.Api/FindAtime.Api.csproj

# Run the tests
dotnet test

# Build / verify
dotnet build
```

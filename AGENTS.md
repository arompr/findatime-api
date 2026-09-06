# AGENTS.md

## What this application is

**Findatime** is a REST API for scheduling events and meetings. It is the backend
of an event-scheduling product. The product's goal is to let people create
scheduling events, share them, and let participants indicate the times they are
available so an organizer can pick a slot that works for everyone.

Current implemented functionality (evolving):

- **Create an event** — POST an event with a name; the API returns a generated
  event id and a location (`/events/{id}`) to share.
- **Retrieve an event** — GET `/events/{id}` returns the event's id and name,
  or 404 when it does not exist.

The data model is intentionally minimal right now (an event is just an id +
name). It is expected to grow with availability/scheduling features (proposed
slots, participant availability, votes, etc.) as the domain is fleshed out.

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
Features/
  Events/
    Api/            HTTP layer (endpoint mapping, request DTOs)
    Domain/         Domain model + factory (Event, EventId, EventFactory)
    Infra/          Persistence (EF Core write side)
      Read/         Read side (raw SQL + Dapper)
    <UseCase>.cs    Application services (CreateEvent, GetEvent)
Common/
  Sql.cs            Loads embedded SQL resources by filename
Program.cs          Composition root / DI registration
```

Layering rules:

- **Api** depends on application services (`CreateEvent`, `GetEvent`).
- **Application** (use-case classes) orchestrates `Domain` factories and `Infra`
  repositories/services; it returns/accepts DTOs.
- **Domain** is pure C# with no EF or HTTP dependencies.
- **Infra** maps domain objects to DB models. The **write** side uses EF Core
  (`DbContext`, `EventRepository`). The **read** side uses a separate
  `ReadDbContext` + Dapper running SQL files loaded via `Sql.Load(...)`.

## Conventions

- Prefer feature slices over cross-cutting folders; add a feature under
  `Features/<FeatureName>/` with the same `Api/Domain/Infra` sub-structure.
- Keep the read and write sides separated (CQRS-lite): writes via EF Core,
  reads via raw SQL + Dapper.
- SQL read queries live in `*.sql` files embedded as resources, referenced by
  filename through `Common/Sql.cs`. New SQL files must be added to the
  `<EmbeddedResource>` items in `findatime-api.csproj`.
- Manual DI registration in `Program.cs` (no `IServiceCollection` extensions so
  far). Register per use-case/service; use the existing lifetime patterns
  (`Scoped` for repositories/services/use-cases, `Singleton` for stateless
  factories).
- Domain entities use typed ids (e.g. `EventId`) rather than raw GUIDs.
- Use records for request/response DTOs (e.g. `CreateEventRequestParams`,
  `EventReadDto`).
- Follow the existing naming: private fields are `_camelCase`, PascalCase public
  members, 4-space indentation (see `.editorconfig`). Do not add comments unless
  asked.

## Database

- PostgreSQL 18 via Podman; local connection string in `appsettings.json`
  (`ConnectionStrings:LocalConnection`, default `localhost:5433`, db/user/pass
  `findatime`). Staging uses `ConnectionStrings:StagingConnection`, which has no
  committed value; it comes from the gitignored `.env` file (loaded via
  DotNetEnv in `Program.cs`) or from deployment env vars (Render). `Program.cs`
  selects the key by environment (`Staging` vs anything else). Never put real
  credentials in committed files.
- EF Core migrations are stored under `Migrations/` and generated/updated with
  the `dotnet ef` CLI.

## Commands

```bash
# Start PostgreSQL
podman compose up -d          # or: podman-compose up -d

# Apply migrations
dotnet ef database update

# Run the API (http profile on :5263)
dotnet run --launch-profile http

# Run the API against the external staging DB (see appsettings.Staging.json)
dotnet run --launch-profile staging

# Apply migrations to the staging DB
dotnet ef database update --environment Staging

# Add a migration
dotnet ef migrations add <Name>

# Build / verify
dotnet build
```

There is currently no test project and no lint/typecheck step beyond
`dotnet build`.

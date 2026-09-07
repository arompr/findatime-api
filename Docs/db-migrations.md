# Database Migrations

Migrations let you evolve the database schema safely and in step with the C# DB
models. EF Core snapshots your models (`Features/Events/Infra/DbContext.cs` and
the `*DbModel.cs` files it exposes as `DbSet`s), diffs them against the last
known state, and generates versioned, checked-in C# files under `Migrations/`
that are applied to the database in order.

A migration always has an `Up` (apply) and a `Down` (revert) method. Each
applied migration is recorded in a `__EFMigrationsHistory` table in the
database, so EF only runs each one once.

## Prerequisites

- PostgreSQL running locally: `podman compose up -d` (listens on `localhost:5433`).
- .NET 10 SDK.
- The `dotnet ef` CLI (backed by the already-included
  `Microsoft.EntityFrameworkCore.Design` package).

## Making a small model change walkthrough

Example: add an optional `description` column to the `Event` model.

### 1. Edit the model

Open `Features/Events/Infra/EventDbModel.cs` and add the property:

```csharp
[Column("description")]
public string? Description { get; set; }
```

The `EventDbModel` is already exposed as a `DbSet` on the `DbContext`, so EF
will notice the change.

### 2. Generate the migration

```bash
dotnet ef migrations add AddEventDescription
```

EF compares your current models against the previous migration and generates a
new migration file under `Migrations/`.

### 3. Review the generated migration

Open the new `Migrations/<timestamp>_AddEventDescription.cs`. Confirm the `Up`
method adds the column and `Down` removes it. If EF generated something
unexpected (wrong column type, unwanted table changes), fix the `Up`/`Down`
here before applying. Do not hand-edit the accompanying `*.Designer.cs` or the
`FindatimeDbContextModelSnapshot.cs` files — those are regenerated for you.

### 4. Apply it locally

```bash
dotnet ef database update
```

This connects to your local DB and applies any pending migrations in order.

### 5. Verify

Check the column exists, e.g.:

```bash
podman exec -it findatime-postgres psql -U findatime -d findatime \
  -c '\d events'
```

Commit the new migration file(s) along with your model
change.

## What happens under the hood

- `dotnet ef migrations add <Name>` snapshots your current models and diffs
  them against the last migration's snapshot to produce a new `Migration` class
  with the generated `Up`/`Down` operations.
- `dotnet ef database update` reads the `__EFMigrationsHistory` table to find
  which migrations have already run, then executes each pending migration's
  `Up` in order (transactionally) and records it in the history table.
- To roll back, apply an earlier migration:
  `dotnet ef database update <previous-migration>`.

## Deploying migrations (staging / production)

When migrations are applied to a deployed database (rather than a local one),
the same mechanism is used against that environment's Postgres. The runner
connects using the deployment's connection string (the
`ConnectionStrings__Postgres` environment variable in the deployed
environment), reads `__EFMigrationsHistory`, applies any pending migrations in
order, and records each one.

> **TODO:** This section is intentionally descriptive only. Migrating a
> deployed database should eventually be wired into a deployment pipeline
> instead of being invoked by hand. Update this
> document once that pipeline exists.

## Final tips

- Run `dotnet ef database update` after adding a migration; a generated
  migration has no effect until it is applied.
- Never hand-edit `*.Designer.cs` or the `ModelSnapshot` files.
- Migration files are checked in — commit them with your model changes.
- EF tracks what has already run in `__EFMigrationsHistory`; do not delete rows
  from it to "re-run" a migration.

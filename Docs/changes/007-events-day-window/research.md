# 007-events-day-window: Research

## Tech stack

- **.NET 11 (net11.0)**, ASP.NET Core minimal APIs, `Nullable` + `ImplicitUsings`.
- **EF Core 11 + Npgsql** for writes and migrations; **Dapper** + embedded `.sql`
  files for reads.
- **PostgreSQL 18** (Podman locally, Testcontainers in integration tests).
- **xunit**: `FindAtime.UnitTests` (pure domain/helpers, OpenAPI document) and
  `FindAtime.IntegrationTests` (use cases resolved from DI, one rolled-back
  transaction per test).
- **Hurl** scratch files per feature; **hey-api** TypeScript client generated from
  the OpenAPI document (`make typescript-client`).

## Relevant modules

- `src/FindAtime.Api/Features/Events/Domain/EventParams.cs` — validating value
  object holding `Timezone` + `IntervalMinutes`; the day window joins it.
- `src/FindAtime.Api/Features/Events/Domain/Event.cs` — `Event.Create` builds the
  default params (`new EventParams(null, Interval.DefaultMinutes)`); `SetParams`
  replaces them wholesale.
- `src/FindAtime.Api/Common/Interval.cs` + `InvalidIntervalException.cs` — the
  pattern to copy: a static helper owning the allowed values and the default, plus
  a dedicated exception mapped in `ExceptionHandler`.
- `src/FindAtime.Api/Features/Events/Application/UpdateEventParams.cs` —
  organizer-only full-replace use case; validates interval then timezone.
- `src/FindAtime.Api/Features/Events/Api/EventsRestService.cs` — `PUT
  /events/{publicId}/params` (inline 400 for missing `intervalMinutes`) and `GET
  /events/{publicId}`.
- `src/FindAtime.Api/Features/Events/Api/UpdateEventParamsRequestParams.cs`,
  `UpdateEventParamsResponse.cs`, `GetEventResponse.cs` — wire DTOs to extend.
- `src/FindAtime.Api/Features/Events/Infra/DbContext.cs` — `ComplexProperty(e =>
  e.Params)` maps the params onto `events` columns with
  `.HasDefaultValue(Interval.DefaultMinutes)`.
- `src/FindAtime.Api/Features/Events/Infra/Read/get_event_by_public_id.sql`,
  `EventDto.cs`, `ReadEventService.cs` — read side of a single event.
- `src/FindAtime.Api/Migrations/20261101173839_AddEventIntervalMinutes.cs` —
  precedent: add a non-null column with a default, no backfill.
- `src/FindAtime.Api/Common/ExceptionHandler.cs` — exception → `(status, error)`
  dictionary.
- `src/FindAtime.Api/Features/Events/Api/events.hurl` — entry 4 exercises the
  params endpoint.
- Tests: `tests/FindAtime.UnitTests/Domain/EventTests.cs`,
  `tests/FindAtime.UnitTests/Common/IntervalTests.cs` (pattern),
  `tests/FindAtime.IntegrationTests/Application/Events/{UpdateEventParamsTests,GetEventTests,CreateEventTests}.cs`,
  `tests/FindAtime.IntegrationTests/Infrastructure/EventRepositoryTests.cs`.

## Existing patterns

- Event-level settings live in `EventParams`; the constructor validates, so an
  invalid value can never be materialized.
- `PUT /events/{publicId}/params` is a **full replace**: every field is required
  in the body; a missing required field is an inline request-shape 400 (`"<field>
  is required"`), an out-of-domain value is an application exception mapped to a
  400 with a machine-readable code.
- Defaults are owned by one constant used by both the domain (`Event.Create`) and
  EF (`HasDefaultValue`) so the DB default and domain default cannot drift.
- Changing params never rewrites stored availability.
- The create request carries no params; new events get defaults.
- `GET /events?guestId=` carries no params.
- Static helpers need no DI registration (no change to
  `ServiceCollectionExtensions` or the integration-test DI copy).

## Feature spec summary

An event has an optional IANA timezone (null = option off, legacy UTC frame) and
a calendar interval of 15/30/60 minutes (default 30). Both are changed together by
the organizer through a full-replace `PUT /events/{publicId}/params` and returned
by `GET /events/{publicId}`. Availability is stored as free-form UTC ranges;
writes snap outward to the interval in the submitter's frame and reads project
inward onto the current interval in the event's anchor frame (event timezone, or
UTC when off). There is no notion today of which hours of the day an event's
calendar covers. The frontend has to pick a fixed range itself.

## Open questions

Resolved with the user on 2026-11-08:

- Enforcement → **display-only**. The window is stored and returned. Availability
  writes and reads ignore it.
- Granularity → **whole hours** (`dayStartHour` 0–23, `dayEndHour` 1–24).
- Default → **09:00–21:00**, for new events and existing rows.
- PUT contract → both fields **required** (full replace, breaking for clients).

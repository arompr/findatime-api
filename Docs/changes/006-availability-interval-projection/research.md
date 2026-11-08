# 006-availability-interval-projection: Research

## Tech stack

.NET 11 / ASP.NET Core minimal APIs, EF Core 11 + Npgsql on the write side,
Dapper + embedded `*.sql` files on the read side, xunit unit and integration
tests (Testcontainers PostgreSQL), Hurl scratch files per feature. Builds and
tests run with `DOTNET_ROOT=/home/ar/.dotnet` because the SDK on `PATH` is 10
and the solution targets `net11.0`.

## Relevant modules

- `src/FindAtime.Api/Common/Interval.cs` — owns the allowed interval set and the
  floor/ceil grid arithmetic (`SnapStart`, `SnapEnd`). The inward projection needs
  the same arithmetic in the opposite direction.
- `src/FindAtime.Api/Common/TimezoneConverter.cs` — `Resolve` / `ToUtc` /
  `ToWallClock`. `ToUtc` throws on DST gaps and ambiguity, which is correct for
  writes but unusable on reads.
- `src/FindAtime.Api/Features/Availability/Application/SetAvailability.cs` — the
  only place the interval is applied today; it snaps outward at write time.
- `src/FindAtime.Api/Features/Availability/Application/GetEventAvailabilities.cs`
  and `GetParticipantAvailability.cs` — both map stored UTC ranges to wall-clock
  without consulting the interval.
- `src/FindAtime.Api/Features/Availability/Infra/Read/ReadAvailabilityService.cs`
  and the three `*.sql` files — the read side; `get_event_availabilities.sql`
  does not select `interval_minutes`.
- `src/FindAtime.Api/Features/Availability/Infra/Read/EventAvailabilityDto.cs` —
  read DTOs (`EventAvailabilityDto`, `ParticipantAvailabilityDto`,
  `AvailabilityRangeDto`).
- `src/FindAtime.Api/Features/Events/Infra/Read/EventDto.cs` — already carries
  `IntervalMinutes` and `Timezone`, which `GetParticipantAvailability` loads.
- `src/FindAtime.Api/Composition/ServiceCollectionExtensions.cs` and
  `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` — manual DI,
  production and test fixture must stay in sync.
- `src/FindAtime.Api/Features/Availability/Api/availability.hurl` — the REST
  scratch workflow.

## Existing patterns

- Static, stateless helpers in `Common/` (`Interval`, `Sql`); instance services
  with `TimezoneConverter` are registered `Singleton` and other use cases
  `Scoped`.
- Reads use Dapper and never write; writes use EF Core and never project.
- Application use cases return `*Result` records; endpoints map them into
  `*Response` records. Response shapes are explicit and hand-maintained.
- Feature behaviour is specified in `Docs/specs/availability/` and changes
  follow the `research → proposal → spec → design → tasks → verification`
  layout established by changes 004 and 005.
- Tests ship with the task that owns the behaviour; unit tests live in
  `tests/FindAtime.UnitTests/Common/`, integration tests under
  `tests/FindAtime.IntegrationTests/Application/Availability/`.

## Feature spec summary

Availability belongs to a participant and is stored as canonical UTC
start/end pairs. A participant submits wall-clock ranges in their IANA
timezone; `SetAvailability` snaps each bound outward onto the event's current
interval grid (15/30/60, default 30) in the submitter's frame, converts to UTC,
and echoes the snapped values. Reads render stored UTC ranges as wall-clock in
the resolved viewer frame (viewer → event → UTC, forced to UTC when the
timezone option is off) and explicitly return stored ranges verbatim — the
event's current interval is not consulted on read and interval changes do not
re-align rows (change 004 REQ-019, availability spec invariant).

## Open questions (resolved with the user before implementation)

- **What does the organizer's interval mean for an already-stored range?**
  Project the stored free time inward onto the current grid: start ceils, end
  floors, and a range that does not contain a full interval contributes nothing.
- **Which frame anchors the grid?** The event's timezone (UTC when the option is
  off), not the viewer's, so every viewer sees the same slot instants.
- **Per-range or union?** Union first — adjacent ranges that jointly cover a
  full interval must produce a slot.
- **Write path?** Unchanged: keep outward snapping and the echoed snapped
  values. The known consequence (a range shorter than one interval submitted
  under a large interval is widened) is accepted.
- **Read contract?** Unchanged shape; no `intervalMinutes` added to availability
  reads.

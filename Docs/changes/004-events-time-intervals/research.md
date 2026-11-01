# 004-events-time-intervals: Research

## Tech stack

- **.NET 11 (net11.0)** — ASP.NET Core minimal APIs (no controllers/MVC), `Nullable` + `ImplicitUsings` enabled, root namespace `findatime_api`.
- **EF Core 11 + Npgsql** — PostgreSQL persistence and migrations.
- **Dapper** — raw SQL reads loaded from embedded `.sql` resources via `Common/Sql.cs`.
- **PostgreSQL** — local container via Podman, migrations under `src/FindAtime.Api/Migrations/`.
- **OpenAPI** — auto-generated via `AddOpenApi()`/`MapOpenApi()`; known limitation: non-standard `QUERY` verb endpoints are not emitted.
- **Tests** — xunit; `FindAtime.UnitTests` for domain/converter unit tests; `FindAtime.IntegrationTests` for use-case + repository tests against Testcontainers.PostgreSQL.

## Relevant modules

### Events feature
- `src/FindAtime.Api/Features/Events/Domain/EventParams.cs` — Value object that currently holds only `Timezone`; designed to grow more params. This is where `IntervalMinutes` will live.
- `src/FindAtime.Api/Features/Events/Domain/Event.cs` — Aggregate root; `Params` is initialized in `Event.Create(..., new EventParams(null))` and replaced wholesale via `SetParams`.
- `src/FindAtime.Api/Features/Events/Application/UpdateEventParams.cs` — Organizer-only use case that full-replaces `EventParams`, validates timezone, and persists.
- `src/FindAtime.Api/Features/Events/Api/EventsRestService.cs` — Maps `PUT /events/{publicId}/params` and `GET /events/{publicId}`; inline validation for `X-Guest-Id`.
- `src/FindAtime.Api/Features/Events/Api/UpdateEventParamsRequestParams.cs` / `UpdateEventParamsResponse.cs` — Request/response DTOs for the params endpoint.
- `src/FindAtime.Api/Features/Events/Api/GetEventResponse.cs` — Response DTO for `GET /events/{publicId}`; must include the interval.
- `src/FindAtime.Api/Features/Events/Infra/DbContext.cs` — EF mapping for `EventParams.Timezone` onto the existing `events.timezone` column via `ComplexProperty`.
- `src/FindAtime.Api/Features/Events/Infra/Read/get_event_by_public_id.sql` — SQL that selects event fields; must add `interval_minutes`.
- `src/FindAtime.Api/Features/Events/Infra/Read/EventDto.cs` / `ReadEventService.cs` — Read-side DTO and service; must surface interval.
- `src/FindAtime.Api/Features/Events/Infra/Read/search_events.sql` / `EventSummaryDto.cs` / `SearchEventsResponse.cs` — List-my-events path; currently does not return timezone or interval.

### Availability feature
- `src/FindAtime.Api/Features/Availability/Application/SetAvailability.cs` — Use case that converts wall-clock ranges to UTC and persists them; this is where interval-alignment validation will be enforced.
- `src/FindAtime.Api/Features/Availability/Api/AvailabilityRestService.cs` — Maps `PUT /events/{publicId}/participants/{participantId}/availability` and the two `QUERY` reads.
- `src/FindAtime.Api/Features/Availability/Api/SetAvailabilityRequest.cs` — Request shape: `{ timezone, ranges: [{ start, end }] }` where `start`/`end` are wall-clock strings without offset.
- `src/FindAtime.Api/Features/Availability/Domain/AvailabilityFactory.cs` — Validates canonical UTC invariants: `end > start` and `end - start <= 24 hours`.
- `src/FindAtime.Api/Features/Availability/Infra/Read/get_event_availabilities.sql` / `get_participant_availability.sql` — Availability read queries; no interval column needed unless we decide to return it there.

### Cross-cutting
- `src/FindAtime.Api/Common/TimezoneConverter.cs` — Stateless singleton: `Resolve`, `ToUtc`, `ToWallClock`. Used when converting submitted wall-clock ranges to UTC and back.
- `src/FindAtime.Api/Common/ExceptionHandler.cs` — Central dictionary mapping exception types to HTTP status + machine-readable `error` code.
- `src/FindAtime.Api/Common/Sql.cs` — Loads embedded SQL resources by filename.
- `src/FindAtime.Api/Composition/ServiceCollectionExtensions.cs` — Manual DI registration; new services (e.g., interval validator) register here and in `IntegrationTest.cs`.

### Tests
- `tests/FindAtime.UnitTests/Domain/EventTests.cs` — Covers `Event.Create` default params and `SetParams` replacement.
- `tests/FindAtime.IntegrationTests/Application/Events/UpdateEventParamsTests.cs` — Covers enable/change/disable timezone, 403, 404, invalid timezone.
- `tests/FindAtime.IntegrationTests/Application/Availability/SetAvailabilityTests.cs` — Covers replace/clear, UTC storage, guest-id mismatch, end-after-start, ≤24h.
- `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` — Manual DI setup for integration tests; must mirror production registrations.

### Scratch files
- `src/FindAtime.Api/Features/Events/Api/events.hurl` — Entry 4 exercises `PUT /events/{publicId}/params`; needs updating for interval.
- `src/FindAtime.Api/Features/Availability/Api/availability.hurl` — Exercises availability reads/writes; may need misalignment cases depending on proposal decision.

## Existing patterns

- **Event-level settings live in `EventParams`**, a value object on the `Event` aggregate, replaced wholesale via `Event.SetParams`. The only current param is `Timezone` (`null` = option off).
- **Full-replace params endpoint** is `PUT /events/{publicId}/params`, organizer-only via `X-Guest-Id`. Non-organizers throw `ForbiddenException` → 403 `forbidden`.
- **Expected failures are application exceptions** mapped in `Common/ExceptionHandler.cs`:
  - `InvalidTimezoneException` → 400 `invalid_timezone`
  - `InvalidAvailabilityRangeException` → 400 `invalid_availability_range`
  - `ForbiddenException` → 403 `forbidden`
  - `EventNotFoundException` → 404 `event_not_found`
  - `ParticipantNotFoundException` → 404 `participant_not_found`
- **Inline `Results.BadRequest` is used for request-shape issues**: missing/blank `X-Guest-Id`, malformed UUID, blank required string fields.
- **Availability writes convert wall-clock → UTC** via `TimezoneConverter.ToUtc` in `SetAvailability`, then run the factory invariants on UTC instants. Reads convert UTC → viewer wall-clock via `TimezoneConverter.ToWallClock`.
- **Read/write separation (CQRS-lite)**: EF Core for writes, Dapper + embedded `.sql` files for reads. New SQL files must be added to `<EmbeddedResource>` items in the csproj.
- **No HTTP-level integration tests** exist; integration tests exercise use cases directly against a Testcontainers PostgreSQL instance.
- **Migrations are EF Core code-first**; any schema change requires `dotnet ef migrations add`.

## Feature spec summary

**Events** are the root aggregate. An organizer creates an event and becomes its first participant; other guests join by public id, optionally supplying a passcode. Event-level settings besides organizer/passcode are grouped in `EventParams`. Today the only param is an optional IANA `Timezone`: `null` means timezone-aware display is off, a non-null id enables it and sets the fallback display frame. The organizer toggles this via `PUT /events/{publicId}/params` (full-replace, idempotent, `X-Guest-Id` required), and `GET /events/{publicId}` returns the current timezone (or `null`).

**Availability** belongs to a participant. Participants submit complete availability as wall-clock ranges in their own IANA timezone; the backend converts each range to a canonical UTC instant, enforces `end > start` and `duration ≤ 24h`, and stores it. Reads render the stored UTC ranges as wall-clock times in a resolved viewer frame (viewer timezone → event timezone → UTC; forced to `UTC` when the event timezone option is off) and return the frame label. The backend owns all timezone math; clients send and render wall-clock strings verbatim.

This change adds a second event-level param: a calendar step `IntervalMinutes` (allowed values 15 / 30 / 60, default 30 on creation) that controls the granularity participants see when picking times. It is organizer-settable via the same full-replace params endpoint, stored with the event, returned on event read, and used by the backend to validate that submitted availability ranges align to it.

## Precedent trace: change 003 (timezone support)

Change 003 threaded a new event param through every layer without a database migration because the `events.timezone` column already existed. The pattern for a net-new param that needs a new column will be similar, with the addition of a migration.

| Layer | What 003 did | Analog for interval |
|-------|-------------|---------------------|
| **Migration** | No migration; reused existing `events.timezone` nullable text column. | Add `events.interval_minutes` integer column (non-null, default 30) via EF migration. |
| **EF write mapping** | `DbContext` maps `EventParams.Timezone` to `timezone` via `entity.ComplexProperty(...)`. | Add `EventParams.IntervalMinutes` and map it to `interval_minutes`. |
| **Domain** | Added `EventParams(string? Timezone)` value object; `Event.Create` defaults to `new EventParams(null)`; `Event.SetParams` replaces it. | Extend `EventParams` with `int IntervalMinutes`; default `30` in `Event.Create`. |
| **Application** | `UpdateEventParams` loads event, checks organizer, resolves timezone, calls `SetParams`, saves. | Extend `UpdateEventParams.Execute` with `int intervalMinutes`, validate allowed values, pass into `SetParams`. |
| **API request/response** | `UpdateEventParamsRequestParams(string? Timezone)`, `UpdateEventParamsResponse(string? Timezone)`. | Extend both with `int IntervalMinutes`; decide required/nullable semantics. |
| **GET event** | `GetEventResponse` added `string? Timezone`; `ReadEventService`/`EventDto`/`get_event_by_public_id.sql` surfaced it. | Add `int IntervalMinutes` to `GetEventResponse`, `EventDto`, and the SQL SELECT. |
| **Endpoint** | `EventsRestService.MapEvents` added `PUT /events/{publicId}/params` with inline `X-Guest-Id` validation and `.Produces(200,400,403,404)`. | Same endpoint; add validation for interval value and possibly a new `.Produces` or error code. |
| **Availability** | `SetAvailability` now converts wall-clock → UTC and stores UTC instants; reads convert back. | `SetAvailability` will additionally validate interval alignment before/after conversion; reads remain unchanged. |
| **DI** | `TimezoneConverter` singleton + `UpdateEventParams` scoped registered in `ServiceCollectionExtensions.cs` and `IntegrationTest.cs`. | Any new interval validator service registers the same way. |
| **Tests** | Added `TimezoneConverterTests`, updated `EventTests`, added `UpdateEventParamsTests`, updated `SetAvailabilityTests`. | Add/update unit + integration tests for params and alignment validation. |
| **Hurl** | Updated `events.hurl` entry 4 and `availability.hurl` to use wall-clock + timezone. | Update `events.hurl` entry 4 to send `intervalMinutes`; add misalignment cases to `availability.hurl` if rejecting. |
| **TS client** | Regenerated via `make typescript-client`. | Regenerate after endpoint/response changes. |

## Current `PUT /events/{publicId}/params` semantics (exact)

- **Request body**: `UpdateEventParamsRequestParams(string? Timezone)` → `{ "timezone": "Europe/Berlin" }` to enable/change, `{ "timezone": null }` to disable.
- **Full-replace**: `UpdateEventParams.Execute` builds `new EventParams(timezone)` and calls `event.SetParams(...)`, replacing the entire `Params` value object.
- **Organizer check**: loads event via `EventRepository.GetByPublicId`, resolves organizer participant, compares `GuestId`; mismatch → `ForbiddenException` → 403 `forbidden`.
- **Timezone validation**: non-null values are resolved via `TimezoneConverter.Resolve`; unknown id → `InvalidTimezoneException` → 400 `invalid_timezone`. Blank/missing `X-Guest-Id` or malformed UUID → inline 400 BadRequest.
- **Response**: `UpdateEventParamsResponse(string? Timezone)` mirrors the request value.
- **GET event**: `GetEventResponse(string PublicId, string Name, bool IsPasscodeProtected, string? Timezone)`.

Adding `IntervalMinutes` means the request/response must carry a second field. Because the operation is a full-replace, the design must decide what an omitted `intervalMinutes` means (likely rejected as missing, since there is no sensible default other than the current value, and full-replace implies the caller supplies the desired state).

## Current availability replace semantics (exact)

- **Endpoint**: `PUT /events/{publicId}/participants/{participantId}/availability`.
- **Request body**: `SetAvailabilityRequest(string Timezone, IReadOnlyList<AvailabilityRangeRequest> Ranges)` where `AvailabilityRangeRequest` is `{ start, end }` wall-clock strings without offset.
- **Validation flow in `AvailabilityRestService`**: inline blank check for `timezone`; `ParseWallClock` rejects malformed/offset-bearing strings by throwing `InvalidAvailabilityRangeException` → 400 `invalid_availability_range`.
- **Use-case flow in `SetAvailability`**: load event + participant, guest-id match → `ForbiddenException` if mismatched; resolve/store participant timezone; convert each wall-clock bound to UTC via `TimezoneConverter.ToUtc` (DST gap/ambiguous → `InvalidAvailabilityRangeException`); pass UTC instants to `AvailabilityFactory.Create`, which enforces `end > start` and `duration ≤ 24h`.
- **Response**: `SetAvailabilityResponse(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResponse> Ranges)` with the ranges converted back to the participant's wall-clock.

Interval-alignment enforcement must fit into this flow. The key technical complication is the **frame of alignment**:

1. **Submitter wall-clock frame** (e.g., "09:00 in my timezone must be on a 30-minute boundary"): easy for the FE to satisfy, but a range that is aligned in `Asia/Kolkata` (+05:30) may convert to a UTC instant that is not aligned to the same grid (09:00 IST = 03:30 UTC).
2. **UTC instant frame** (e.g., "the stored UTC start/end must be on a 30-minute boundary"): simple to implement after `ToUtc`, but it makes non-hour-offset timezones effectively incompatible with coarse intervals; e.g., a participant in `Asia/Kolkata` could never submit a 60-minute-aligned range because 03:30 UTC is not on an hour boundary.
3. **Both frames**: aligned in submitter wall-clock *and* UTC; this is the most restrictive and likely surprising to users in non-hour-offset zones.

## API surface inventory (for the FE handoff doc)

| Endpoint | Method | Auth | Request | Response | Relevant error codes |
|----------|--------|------|---------|----------|---------------------|
| Create event | `POST /events` | — | `{ name, guestId, organizerName, isPasscodeProtected }` | 201 `{ publicId, isPasscodeProtected, passcode? }` | 400 malformed/missing fields |
| Get event | `GET /events/{publicId}` | — | — | 200 `{ publicId, name, isPasscodeProtected, timezone, intervalMinutes }` | 404 `event_not_found` |
| Update params | `PUT /events/{publicId}/params` | `X-Guest-Id` (organizer) | `{ timezone, intervalMinutes }` | 200 `{ timezone, intervalMinutes }` | 400 invalid fields / `invalid_timezone` / `invalid_interval`, 403 `forbidden`, 404 `event_not_found` |
| Join event | `POST /events/{publicId}/join` | — | `{ passcode?, guestId, participantName, timezone }` | 201 `{ participantId, name }` | 400 `timezone is required` / `invalid_timezone`, 401 `invalid_passcode`, 404 `event_not_found`, 409 `participant_already_joined` |
| Resolve participant | `GET /events/{publicId}/participant` | `X-Guest-Id` | — | 200 `{ participantId, name }` | 400 bad header, 404 `participant_not_found` / `event_not_found` |
| List availability | `QUERY /events/{publicId}/availabilities` | — | `{ timezone? }` | 200 `{ timezone, participants: [{ participantId, name, ranges: [{ start, end }] }] }` | 400 `invalid_timezone`, 404 `event_not_found` |
| Get one availability | `QUERY /events/{publicId}/participants/{participantId}/availability` | — | `{ timezone? }` | 200 `{ participantId, name, timezone, ranges: [{ start, end }] }` | 400 `invalid_timezone`, 404 `event_not_found` / `participant_not_found` |
| Set availability | `PUT /events/{publicId}/participants/{participantId}/availability` | `X-Guest-Id` (participant) | `{ timezone, ranges: [{ start, end }] }` | 200 `{ participantId, name, timezone, ranges: [{ start, end }] }` | 400 `timezone is required` / `invalid_timezone` / `invalid_availability_range` / `invalid_interval_alignment?`, 403 `forbidden`, 404 `event_not_found` / `participant_not_found` |

*Fields marked with `?` are the anticipated additions; error codes in italics are open design decisions.*

## Test landscape

- **Unit tests** belong in `tests/FindAtime.UnitTests/`:
  - `Domain/EventTests.cs` — update `Create_ShouldDefaultParamsToNullTimezone` to also assert default `IntervalMinutes == 30`; update `SetParams_ShouldReplaceParams` to include interval.
  - New or extended tests for interval-alignment logic if it is extracted into a pure helper (recommended for unit-testability).
- **Integration tests** belong in `tests/FindAtime.IntegrationTests/`:
  - `Application/Events/UpdateEventParamsTests.cs` — add cases for invalid interval, interval persistence, and full-replace behavior.
  - `Application/Events/GetEventTests.cs` or read-service tests — assert `IntervalMinutes` is returned.
  - `Application/Availability/SetAvailabilityTests.cs` — add aligned/misaligned range cases (the exact assertions depend on the reject-vs-snap decision).
  - `Fixtures/IntegrationTest.cs` — register any new service (e.g., interval validator) so it resolves in tests.
- **Scratch files**:
  - `events.hurl` entry 4 must send `intervalMinutes` and assert it in the response.
  - `availability.hurl` should include an interval-misalignment case if the proposal chooses rejection.
- There are no HTTP endpoint integration tests using `WebApplicationFactory`; the project tests at the use-case/repository layer.

## Open questions / risks

1. **Reject vs snap misaligned ranges** (user-flagged scope decision for proposal):
   - **Reject**: simpler contract; FE already knows the interval and can snap client-side before sending. Risk: participants in non-hour-offset timezones may find coarse intervals (60 min) effectively unusable if alignment is checked in UTC.
   - **Snap**: backend silently adjusts submitted ranges to the nearest interval boundary. Risk: surprising side effects (a participant's 09:05 selection becomes 09:00 or 09:15); must define rounding direction and document it.

2. **Alignment frame** (closely related to #1):
   - Validate alignment in the **submitter's wall-clock timezone**? This matches the picker UI but allows UTC instants that fall on fractional boundaries.
   - Validate alignment in **UTC instants**? Simple after `ToUtc` but harsh for zones like `Asia/Kolkata` (+05:30) or `Asia/Kathmandu` (+05:45).
   - Validate in **both frames**? Maximally strict; may reject ranges that look correct to the participant.

3. **Request shape for `PUT /events/{publicId}/params`**:
   - The operation is a full-replace. Should `intervalMinutes` be required (client must send both `timezone` and `intervalMinutes`), or should omitting it preserve the current value? The latter contradicts full-replace semantics but may be more ergonomic.

4. **Error code for invalid interval**:
   - New `InvalidIntervalException` → 400 `invalid_interval`?
   - Reuse `InvalidAvailabilityRangeException` → 400 `invalid_availability_range` for both param value and range alignment?
   - Separate codes for "invalid interval value" vs "range not aligned to interval"?

5. **Default interval for existing events**:
   - The brief says "default 30min on creation (no change to POST /events)". Existing events created before this change will have a null `interval_minutes` column. The migration can either (a) add the column with a default of 30 so all existing rows become 30, or (b) leave it nullable and treat null as 30 in code. A non-null default is cleaner and matches the "default" intent.

6. **Where else to return `intervalMinutes`**:
   - The brief requires it only on `GET /events/{publicId}`. Should `GET /events?guestId=` (list) also include it so the FE can render picker steps without a second round-trip? This is a FE-ergonomics question, not a backend correctness one.

7. **Behavior when the organizer changes the interval after availability is submitted**:
   - Existing ranges may no longer align to the new interval. Should reads still return them unchanged? Should the FE be prepared to display legacy ranges? No requirement in the brief, but worth documenting.

8. **TypeScript client / OpenAPI**:
   - Adding `intervalMinutes` to `GetEventResponse` and the params DTOs will flow into the generated client. The two `QUERY` availability reads will remain absent from the generated service (pre-existing limitation); the FE handoff doc must call this out if the FE relies on generated methods for those endpoints.

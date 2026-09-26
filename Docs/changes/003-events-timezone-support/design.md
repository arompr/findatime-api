# 003-events-timezone-support: Design

## Summary

Activate the dormant timezone scaffolding (`events.timezone`,
`participants.timezone` — both nullable text, always null today) so that:

- every participant's timezone is captured (join + availability-submit) and
  availability ranges are submitted as **local wall-clock** times in that
  timezone, then stored as **canonical UTC instants**;
- an event-level "timezone option" (the nullable `events.timezone` value:
  null = off, IANA id = on + fallback frame) toggles read rendering after
  creation via a new `PUT /events/{publicId}/params`;
- when the option is on, reads convert each stored instant into the **viewer's**
  timezone and return a timezone label; when off, reads return UTC wall-clock
  with label `UTC` (the raw instant, legacy behavior). The backend does all
  conversion; the frontend renders the wall-clock strings it receives verbatim.

No database migration is required — the columns and `timestamp with time zone`
storage already exist and are already UTC-normalized by Npgsql.

## Resolved decisions

### D1 — Wire format: IANA timezone id, not UTC offset

Every timezone identity (event timezone, participant timezone, and the returned
timezone label) is a **full IANA id** (`Europe/Berlin`), never a raw offset.

**Rationale:** offsets do not encode DST rules, so a fixed `+02:00` is wrong for
winter dates; an IANA id resolves unambiguously for any instant. Browsers already
produce an IANA id via `Intl.DateTimeFormat().resolvedOptions().timeZone`, and
.NET 8+ resolves IANA ids on Linux via `TimeZoneInfo.FindSystemTimeZoneById`.

- **Submit (write):** ranges are sent as local wall-clock ISO-8601 datetimes
  **without offset** (`"2026-09-25T18:00:00"`), interpreted in the participant's
  timezone, which is sent alongside (always). The request DTO range fields are
  `string`; the use case parses them as `DateTimeKind.Unspecified`.
- **Read:** ranges are returned as wall-clock `DateTime` (Unspecified →
  serialized as `"2026-09-25T12:00:00"`, no offset) plus a single frame label.
  The FE renders the string + label and does no conversion.

The FE never converts: it supplies `(wall-clock, its own tz)` on write and
renders `(wall-clock, label)` on read.

### D2 — Toggle = the nullable `events.timezone` value, exposed via `EventParams`

The event-level option is the nullable `events.timezone` value itself
(`null` = off, non-null IANA id = on + fallback display frame). No separate
boolean. It is modelled in the domain as an **`EventParams`** value object that
currently holds only `Timezone` and is designed to grow more params later.

**Rationale:** the column already exists and a non-null value already carries
both "on" and "the fallback frame". A separate boolean would permit the
contradictory state (`on=true, value=null`) and force a migration. Wrapping the
single value in `EventParams` gives event-level settings a stable, full-replace
shape that won't need another endpoint as more params appear.

Semantics:

- **Create** always leaves `timezone = null` (off). `POST /events` is unchanged.
- **Enable/change/disable after creation** via a new endpoint
  `PUT /events/{publicId}/params` (full-replace, idempotent) with body
  `{ "timezone": "<IANA>" }` to enable/change, or `{ "timezone": null }` to
  disable. Requires an `X-Guest-Id` header matching the organizer (else 403
  `forbidden`). Returns `200 { timezone }` with the resulting params. This is the
  only new endpoint; there is still no generic event-edit endpoint.

### D3 — Participant timezone captured at join AND availability-submit

Both capture points record the timezone (the FE always supplies it):

- **Join** `POST /events/{publicId}/join` gains a **required** `timezone`
  (IANA id), stored on the new participant.
- **SetAvailability** `PUT …/availability` gains a **required** `timezone`; the
  use case **updates** the participant's stored timezone to the supplied value
  before interpreting the ranges. This refreshes a stale timezone (participant
  moved) without a separate endpoint.
- **Create** stays unchanged: the organizer's timezone is captured at their
  first availability-submit (required `timezone` there), and no feature reads the
  organizer's stored timezone (viewer timezone is supplied per-read, see D4).

### D4 — Conversion points, validation, and the fallback chain

- **Write (wall-clock → UTC):** `SetAvailability` — for each range, parse
  wall-clock, then `TimeZoneInfo.ConvertTimeToUtc` using the participant's
  timezone, producing a UTC `DateTimeOffset` (offset 0) that is stored.
- **Read (UTC → wall-clock):** `GetEventAvailabilities` and
  `GetParticipantAvailability` — when the option is on, convert each stored
  instant to the **viewer frame**; when off, the frame is `UTC`. The same
  `ToWallClock(instant, frame)` path serves both (off = frame `UTC`).

**Viewer frame resolution (fallback chain):** the viewer timezone is an optional
body field `timezone` on the two **`QUERY`** read endpoints —
`QUERY /events/{publicId}/availabilities` and
`QUERY /events/{publicId}/participants/{participantId}/availability` — sharing
`AvailabilityQueryRequest(string? Timezone)` bound via `[FromBody]`. This is a
verb change GET → QUERY (body-carrying). Minimal APIs do not ship a `MapQuery`
extension (only `MapGet`/`MapPost`/… and `MapMethods`), so the routes are
registered with `app.MapMethods(pattern, [HttpMethods.Query], handler)`;
`HttpMethods.Query`/`IsQuery` are available in .NET 10. Frame = body `timezone`
(if non-null) → `events.timezone` (if non-null) → `UTC`.

**Rejection rules** (present-but-invalid ⇒ typed exception; blank/missing ⇒
inline `Results.BadRequest`, matching the repo convention):

- Non-empty but unknown IANA id (join/create-params/SetAvailability timezone, or
  the read `timezone` body field) → **`InvalidTimezoneException`** →
  `400 invalid_timezone`.
- A range whose wall-clock value is malformed, offset-bearing, ambiguous, or
  nonexistent in the participant's timezone (DST gap), or which violates
  end-after-start / ≤24h → **`InvalidAvailabilityRangeException`** →
  `400 invalid_availability_range` (existing exception reused).
- Blank required `timezone`, missing/blank `X-Guest-Id`, malformed uuid → inline
  `Results.BadRequest` (existing style, no `error` code).

**Null-timezone handling:** a participant with null `timezone` is only relevant
on write, and the submit always carries a `timezone`, so no conversion ever runs
against a null value. A null `events.timezone` means "option off" (read as UTC)
or, when no viewer `timezone` is supplied, the fallback lands on `UTC`.

### D5 — Persistence / migration

**No migration.** `events.timezone` and `participants.timezone` already exist
(nullable text); `availabilities.start_utc`/`end_utc` are `timestamp with time
zone` (UTC-normalized by Npgsql). The toggle reuses the existing nullable
column (now surfaced through `EventParams.Timezone`), so no boolean column,
constraint, or index changes are needed. The only storage-related change is
behavioral: the write path now produces UTC offset-0 `DateTimeOffset`s
explicitly (conversion happens before the factory), and the ≤24h invariant now
applies to the canonical UTC instants (unchanged invariant, now measured on real
elapsed time).

### D6 — API contract delta (enumerated)

See the "API" area below. New error code: `invalid_timezone`. New endpoint:
`PUT /events/{publicId}/params`. The two availability reads change verb
GET → QUERY (body-carrying), keeping their original paths, via
`MapMethods([HttpMethods.Query])` + `[FromBody]` (CORS already allows QUERY).
Every read/write of ranges moves from `DateTimeOffset` to wall-clock
`DateTime`/`string` + a timezone label.

## Changes

### Common (new, cross-cutting)

- **`Common/TimezoneConverter.cs`** (stateless, `Singleton`):
  - `TimeZoneInfo Resolve(string ianaId)` — validates via
    `TimeZoneInfo.FindSystemTimeZoneById`, throws `InvalidTimezoneException`.
  - `DateTimeOffset ToUtc(DateTime wallClock, string ianaId)` — converts an
    `Unspecified` wall-clock to UTC; throws `InvalidAvailabilityRangeException`
    when the local time is invalid (DST gap) or ambiguous.
  - `DateTime ToWallClock(DateTimeOffset instant, string ianaId)` — converts a
    UTC instant to an `Unspecified` wall-clock `DateTime` in the frame.
- **`Common/InvalidTimezoneException.cs`** (new) — message
  `"Timezone 'X' is not a valid IANA timezone."`
- **`Common/ExceptionHandler.cs`** — add to the mapping dictionary:
  `InvalidTimezoneException → (400, "invalid_timezone")`. No `if` chains.

### Domain

- **`Features/Events/Domain/EventParams.cs`** (new) —
  `public record EventParams(string? Timezone);` (designed to grow further
  params later).
- `Features/Events/Domain/Event.cs` — replace the `string? Timezone` property
  with `public EventParams Params { get; private set; }` (initialized to
  `new EventParams(null)`); add `public void SetParams(EventParams @params)`.
- `Features/Events/Domain/Participant.cs` — add `public void SetTimezone(string? timezone)`
  (property already has a setter; method makes the write intentional).
- `Features/Events/Domain/ParticipantFactory.cs` — `CreateParticipant(..., string? timezone)`
  now accepts and stores the timezone.
- `Features/Availability/Domain/AvailabilityFactory.cs` — unchanged (still
  receives UTC `DateTimeOffset`s and enforces the two invariants). Conversion
  happens upstream in the use case.

### Application

- `Features/Events/Application/JoinEvent.cs` — new `timezone` parameter; after
  the duplicate-join guard, `TimezoneConverter.Resolve(timezone)` (throws
  `InvalidTimezoneException`) then `participant.SetTimezone(timezone)`.
- **`Features/Events/Application/UpdateEventParams.cs`** (new) —
  `Execute(string publicId, Guid guestId, string? timezone)`:
  1. `EventRepository.GetByPublicId(publicId)` (includes participants) → 404 via
     `EventNotFoundException`.
  2. Resolve the organizer participant via `OrganizerParticipantId`; if the
     caller's `guestId` does not match the organizer's `GuestId`, throw
     `ForbiddenException` (403 `forbidden`).
  3. If `timezone` is non-null, `TimezoneConverter.Resolve(timezone)`.
  4. `event.SetParams(new EventParams(timezone))`; `EventRepository.Save`; return
     `UpdateEventParamsResponse(timezone)`.
- `Features/Availability/Application/SetAvailability.cs` — new `timezone`
  parameter. Resolve participant (existing), verify guest id, then
  `TimezoneConverter.Resolve(timezone)` + `participant.SetTimezone(timezone)`;
  for each range parse wall-clock → `ToUtc(..., timezone)` → factory. Return the
  ranges converted back to the participant's wall-clock + `timezone` label.
- `Features/Availability/Application/GetEventAvailabilities.cs` and
  `GetParticipantAvailability.cs` — new `string? viewerTimezone` parameter
  (from the request body). Compute `frame = viewerTimezone ?? event.Timezone ??
  "UTC"`; when the event timezone is null (off) force `frame = "UTC"`. Convert
  each raw UTC range via `ToWallClock(instant, frame)`.
  `GetParticipantAvailability` additionally reads the event timezone (now on
  `EventDto`).
- `GetMyParticipant` — unchanged.

### API

- `Features/Events/Api/JoinEventRequestParams.cs` — add required
  `string Timezone`.
- `Features/Events/Api/GetEventResponse.cs` — add `string? Timezone`.
- **`Features/Events/Api/UpdateEventParamsRequestParams.cs`** (new) —
  `record(string? Timezone)`.
- **`Features/Events/Api/UpdateEventParamsResponse.cs`** (new) —
  `record(string? Timezone)`.
- `Features/Events/Api/EventsRestService.cs`:
  - `POST /events/{publicId}/join` — inline blank check for `timezone`
    (`"timezone is required"`).
  - **`PUT /events/{publicId}/params`** — read `X-Guest-Id` header
    (blank/malformed → inline `Results.BadRequest`), call `UpdateEventParams`,
    return `200 UpdateEventParamsResponse`. `.Produces(200, 400, 403, 404)`.
- **`Features/Availability/Api/AvailabilityQueryRequest.cs`** (new, shared) —
  `record(string? Timezone)`.
- `Features/Availability/Api/SetAvailabilityRequest.cs` — become
  `record SetAvailabilityRequest(string Timezone, IReadOnlyList<AvailabilityRangeRequest> Ranges)`
  and `record AvailabilityRangeRequest(string Start, string End)` (wall-clock
  strings, no offset).
- `Features/Availability/Api/SetAvailabilityResponse.cs` — become
  `record(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResponse> Ranges)`.
- `Features/Availability/Api/GetEventAvailabilitiesResponse.cs` — top-level
  `EventTimezone` becomes `string Timezone` (the resolved frame label);
  `AvailabilityRangeResponse` becomes `record(DateTime Start, DateTime End)`
  (wall-clock, `DateTimeKind.Unspecified`).
- **`Features/Availability/Api/GetParticipantAvailabilityResponse.cs`** (new) —
  `record(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResponse> Ranges)`
  (the single-read response now carries its own frame label, distinct from the
  list item type).
- `Features/Availability/Api/AvailabilityRestService.cs`:
  - Replace the two GET reads with body-carrying **`QUERY`** endpoints on their
    original paths (no `/query` suffix):
    - **`QUERY /events/{publicId}/availabilities`** — body
      `AvailabilityQueryRequest` bound `[FromBody]`; validates a non-null
      `Timezone` via the converter (present-but-invalid → 400
      `invalid_timezone`), else passes null.
    - **`QUERY /events/{publicId}/participants/{participantId}/availability`** —
      body `AvailabilityQueryRequest` bound `[FromBody]`; same validation.
    - Register with `app.MapMethods(pattern, [HttpMethods.Query], handler)`
      (there is no `MapQuery` extension; `HttpMethods.Query` is available in
      .NET 10). No CORS change — the default policy uses `AllowAnyMethod()`.
  - `PUT …/availability` — inline blank check for `timezone`; map the new
    request/response DTOs.

### Infra / Read

- `Features/Events/Infra/DbContext.cs` — map `EventParams` as a complex property
  so `EventParams.Timezone` targets the existing `events.timezone` column
  (`entity.ComplexProperty(e => e.Params, c => c.Property(p => p.Timezone).HasColumnName("timezone"))`);
  remove the old `[Column("timezone")]` on `Event`. No migration.
- `Features/Events/Infra/Read/get_event_by_public_id.sql` — add `timezone` to
  the SELECT (`SELECT public_id, name, timezone, (passcode_hash IS NOT NULL) AS is_passcode_protected`).
- `Features/Events/Infra/Read/EventDto.cs` — add `string? Timezone`.
- `Features/Events/Infra/Read/ReadEventService.cs` — read the nullable
  `timezone` column (`IsDBNull → null`).
- `Features/Availability/Infra/Read/*` — unchanged: `get_event_availabilities.sql`
  already selects the event `timezone`; `get_participant_availability.sql` needs
  no timezone (participant timezone is write-only). `AvailabilityRangeDto`
  stays `DateTimeOffset` (raw UTC); conversion moves to the use cases.
- `Features/Availability/Application/GetEventAvailabilitiesResult.cs`,
  `SetAvailabilityResult.cs`, and the `ParticipantAvailabilityResult` shape — add
  the timezone label and switch ranges to wall-clock `DateTime`.

### Composition

- `Composition/ServiceCollectionExtensions.cs` — register `TimezoneConverter`
  (`Singleton`), `UpdateEventParams` (`Scoped`); inject `TimezoneConverter`
  into `JoinEvent`, `SetAvailability`, `GetEventAvailabilities`,
  `GetParticipantAvailability`.

## Migration / Rollout

No database migration. Steps:

1. Apply the code changes; run `dotnet build` and `dotnet test`.
2. Regenerate the TypeScript client (`make typescript-client`) so the FE's
   request/response models reflect the new wall-clock + timezone shape and the
   new/renamed endpoints.
3. This is a **breaking wire-format change** for availability reads/writes and
   join (ranges move from `DateTimeOffset` to wall-clock strings + timezone;
   `EventTimezone` becomes a resolved frame label; the two reads change verb
   GET → QUERY, keeping their paths; `PATCH …/timezone` becomes
   `PUT …/params`; `GetEventResponse` gains `timezone`). Ship the backend and
   regenerated FE in lockstep.
4. Roll back cleanly: existing null-timezone rows remain valid — with the option
   off (null), reads return UTC wall-clock and nothing converts; enabling the
   option later requires no backfill (participant timezones are captured from
   this point forward).

# Availability Design

## Overview

Availability is a separate aggregate within the Scheduling bounded context. It
references Participant by ParticipantId. Availability is exchanged and stored
as UTC instants. All wall-clock math happens in the stateless
`Common/AvailabilityProjection` helper, in the event's timezone: `SnapOutward`
on write and `Project` on read. `Common/Interval` owns the event interval's
allowed set, default, and floor/ceil arithmetic. `TimezoneConverter` only
resolves and validates IANA ids; it is shared with the Events feature.

## Domain Model

    Participant 1 ──── 0..* Availability

    Availability
        - Start (canonical UTC instant)
        - End (canonical UTC instant)

Availability is a pure C# entity; the factory enforces the invariants on the
canonical UTC instants. Ownership flows Availability → Participant → Event, so
an availability range references only ParticipantId (no redundant EventId).

Granularity and anchor are event-level params, read from the event that
`SetAvailability` already loads: `Event.Params.IntervalMinutes` (15, 30, or 60,
default 30) and `Event.Params.Timezone ?? "UTC"` (owned by the Events feature).
Write and read use the same anchor rule, so a snapped write projects to itself.

### Why the server snaps in the event frame (008)

Before 008 the API exchanged wall-clock datetimes plus a viewer timezone. Each
side converted with its own tz database (server tzdata vs browser ICU), and
writes snapped in the viewer's frame while reads projected in the event's.
When the two databases disagreed (British Columbia's permanent UTC-7 from
Nov 1 2026) blocks drifted an hour. Half-hour-offset viewers could also lose
saved ranges on read. Now instants cross the wire unchanged, and all grid math
uses the event's frame on the server, so a client converts only for display.

## API

`PUT /events/{publicId}/participants/{participantId}/availability` replaces all
availability ranges for the participant. The body is `{ ranges }` with
offset-bearing ISO 8601 instants; `AvailabilityRestService.ParseInstant` rejects
offset-less values with `InvalidAvailabilityRangeException` and normalizes to
UTC.

The calling participant is resolved via
`GET /events/{publicId}/participant` (using the `X-Guest-Id` header).

Reads are plain `GET`s with no body: `GET /events/{publicId}/availabilities`
(all) and `GET /events/{publicId}/participants/{participantId}/availability`
(one). Range DTOs are `DateTimeOffset`, so the OpenAPI document emits
`format: date-time` and the generated TypeScript client has SDK functions for
every availability operation.

## Persistence

`availabilities` table:
    - id
    - participant_id
    - start_utc
    - end_utc
    - created_at

The table enforces `end_utc > start_utc` and `end_utc - start_utc <= 24 hours`
as check constraints (mirrored by the factory validation).
`availabilities.participant_id` references `participants.id` with
`ON DELETE CASCADE`: a participant's ranges are removed with the participant, and
so are an event's ranges when the event is deleted (see the Events design).

`events.timezone` (the grid anchor), `events.interval_minutes` (the event's
calendar step, non-null, default 30), and `participants.timezone` (the
participant's IANA timezone, set at join only) live on the events side.
Availability adds no granularity column: the grid is read from the event at
write time, so stored ranges keep the values they were written with and are
never rewritten when the interval later changes; reads project them inward onto
the interval current at read time. `availabilities` rows are
`timestamp with time zone`, and Npgsql requires offset-0 values, so every
instant is passed through `ToUniversalTime()` before it reaches EF.

Rows written before 008 were snapped in the submitter's frame. They need no
migration: `Project` re-snaps them in the event frame on read.

## Validation

- Start must precede end (checked before snapping and again by the factory).
- Duration must not exceed 24 hours, checked on the **snapped** range; it is
  never clamped.
- Bounds must be ISO 8601 instants with an explicit offset; a malformed or
  offset-less value is `invalid_availability_range`.
- Participant must belong to the specified event.
- The `X-Guest-Id` header must match the participant's guest id.

## Application Flow

Reads: API → `GetEventAvailabilities` / `GetParticipantAvailability` →
`ReadEventService` + `ReadAvailabilityService` (Dapper raw SQL) → PostgreSQL.
Both project each participant's stored ranges with
`AvailabilityProjection.Project` using the event's current `interval_minutes`
and an anchor of `event timezone ?? "UTC"`, and return the projected instants as
UTC `DateTimeOffset`s. `GetEventAvailabilities` takes the interval from its own
SQL; `GetParticipantAvailability` already has it on `EventDto`.

Writes: API → `SetAvailability` → `EventRepository.GetByPublicId` (loads the
`Event` aggregate with participants) + `AvailabilityRepository` (EF Core) →
PostgreSQL. `SetAvailability` checks end > start, snaps each range with
`AvailabilityProjection.SnapOutward(range, interval, anchor)`, builds the
entities through `AvailabilityFactory`, and returns the stored instants. It does
not touch the participant's timezone.

`SnapOutward` converts each bound to anchor wall clock, applies
`Interval.Floor` (start) / `Interval.Ceil` (end), and converts back with the
same lenient DST resolution `Project` uses. On a repeated hour it picks the
interpretation that keeps the range from shrinking: the latest one at or before
the original start, and the earliest one at or after the original end.

The replace semantics live in the use case, not the repository:
`SetAvailability` resolves the participant via `Event.FindParticipant`,
validates the guest id, then calls `AvailabilityRepository.DeleteForParticipant`
followed by `AvailabilityRepository.Save`. The repository exposes primitive
persistence ops only; atomicity comes from a single `SaveChanges` commit in
`Save` (which also flushes the staged delete).

## Testing

- Domain validation → unit tests
- Timezone id resolution → unit tests
- Grid floor/ceil arithmetic (exact boundaries, cross-midnight) → unit tests, no DI
- Outward write snapping (event-frame anchoring, half-hour-offset input, DST
  change, UTC anchor, offset normalization) and inward read projection (union,
  short ranges, DST) → `AvailabilityProjectionTests`, no DI
- Instant parsing (offset required, UTC normalization) → `ParseInstantTests`
- OpenAPI shape (GET reads, `date-time` ranges, no timezone field) →
  `OpenApiDocumentTests`
- SetAvailability / read use cases → integration tests, including aligned
  pass-through, outward snap and echo, the half-hour-offset round trip, the
  legacy UTC anchor, participant timezone unchanged by a save, rejection of a
  snapped range over 24 hours, projection after an interval change in both
  directions, and participants whose ranges all project away
- Wire level → the `availability.hurl` workflow (instant round trip, offset-less
  400, interval switch and projection)

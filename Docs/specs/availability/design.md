# Availability Design

## Overview

Availability is a separate aggregate within the Scheduling bounded context. It
references Participant by ParticipantId. Availability is stored as canonical UTC
instants; wall-clock ↔ UTC conversion is centralized in a stateless
`TimezoneConverter` singleton shared with the Events feature, and grid snapping
in the stateless `Common/Interval` helper that owns the event interval's allowed
set, default, and floor/ceil arithmetic.

## Domain Model

    Participant 1 ──── 0..* Availability

    Availability
        - Start (canonical UTC instant)
        - End (canonical UTC instant)

Availability is a pure C# entity; the factory enforces the invariants on the
canonical UTC instants. Ownership flows Availability → Participant → Event, so
an availability range references only ParticipantId (no redundant EventId). The
`TimezoneConverter` (`Resolve`/`ToUtc`/`ToWallClock`) is the single conversion
point: `SetAvailability` converts wall-clock input to UTC before the factory,
and the read use cases convert stored UTC back to wall-clock in the resolved
frame.

Granularity is an event-level param, read from the event that `SetAvailability`
already loads: `Event.Params.IntervalMinutes` (15, 30, or 60, default 30, owned
by the Events feature). Before conversion, `SetAvailability` snaps each bound
with `Interval.SnapStart`/`SnapEnd` — start floored down, end ceiled up on a grid
anchored at midnight of the wall-clock date. Snapping is done in the submitter's
wall-clock frame, not the UTC frame, because that is the frame the participant
saw and because a non-hour-offset zone (e.g. `+05:30`) could otherwise never
produce a UTC instant on an hour grid. The factory invariants and the `ToUtc`
DST gap/ambiguous checks therefore run on the snapped values: a snapped range
that is non-increasing or longer than 24 hours is rejected as
`invalid_availability_range`, never clamped.

Participant resolution is done through the `Event` aggregate: `Event` exposes
`FindParticipant(ParticipantId)` and, for writes, is loaded with its
participants via `EventRepository.GetByPublicId` (not a separate read query).

## API

`PUT /events/{publicId}/participants/{participantId}/availability`

The PUT operation replaces all availability ranges for the participant; the body
supplies the participant's IANA `timezone` and wall-clock ranges (no offset).

The calling participant is resolved via
`GET /events/{publicId}/participant` (using the `X-Guest-Id` header).

Reads are served via `QUERY /events/{publicId}/availabilities` (all) and
`QUERY /events/{publicId}/participants/{participantId}/availability` (one). Both
take an optional viewer `timezone` in the body and return wall-clock ranges in
the resolved frame (viewer → event → UTC, forced to UTC when the event's
timezone option is off) plus the frame label. The non-standard `QUERY` verb is
registered via `MapMethods(..., [HttpMethods.Query], ...)`.

## Persistence

`availabilities` table:
    - id
    - participant_id
    - start_utc
    - end_utc
    - created_at

The table enforces `end_utc > start_utc` and `end_utc - start_utc <= 24 hours`
as check constraints (mirrored by the factory validation).

`events.timezone` (the event's timezone option / fallback frame),
`events.interval_minutes` (the event's calendar step, non-null, default 30), and
`participants.timezone` (the participant's IANA timezone) all live on the events
side; `participants.timezone` is captured at join/availability-submit and
`events.timezone` is set by the organizer's params toggle. Availability adds no
granularity column: the grid is read from the event at write time, so stored
ranges keep the values they were written with and are never re-snapped when the
interval later changes. `availabilities` rows are `timestamp with time zone` and
are always written as offset-0 UTC instants.

## Validation

- Start must precede end.
- Duration must not exceed 24 hours (measured on the canonical UTC instants).
- Both invariants above are checked on the **snapped** values: each submitted
  wall-clock bound is first floored (start) or ceiled (end) onto the event's
  interval grid in the submitter's timezone, and the snapped range is rejected as
  `invalid_availability_range` if it is non-increasing or over 24 hours; it is
  never clamped.
- The participant's `timezone` is required and must be a known IANA id
  (`invalid_timezone`); a range that is malformed, offset-bearing, ambiguous, or
  nonexistent in that timezone is rejected as `invalid_availability_range`. The
  ambiguity/gap check applies to the snapped bounds.
- A viewer `timezone` supplied in a read body must be a known IANA id.
- Participant must belong to the specified event.
- The `X-Guest-Id` header must match the participant's guest id.

## Application Flow

Reads: API → `GetMyParticipant` / `GetEventAvailabilities` /
`GetParticipantAvailability` → `ReadEventService` + `ReadAvailabilityService`
(Dapper raw SQL) → PostgreSQL. The two availability reads resolve the viewer
frame (viewer → event → UTC, forced to UTC when the option is off) and convert
each raw UTC range via `TimezoneConverter.ToWallClock`.

Writes: API → `SetAvailability` → `EventRepository.GetByPublicId` (loads the
`Event` aggregate with participants) + `AvailabilityRepository` (EF Core) →
PostgreSQL. `SetAvailability` resolves and stores the participant's timezone,
snaps each wall-clock bound with `Interval.SnapStart`/`SnapEnd` using the loaded
event's `Params.IntervalMinutes` (no extra query), converts the snapped bounds to
UTC via `TimezoneConverter.ToUtc`, and returns the ranges converted back via
`ToWallClock` — so the response echoes the snapped values with no separate code
path.

The replace semantics live in the use case, not the repository:
`SetAvailability` resolves the participant via `Event.FindParticipant`,
validates the guest id, then calls `AvailabilityRepository.DeleteForParticipant`
followed by `AvailabilityRepository.Save`. The repository exposes primitive
persistence ops only; atomicity comes from a single `SaveChanges` commit in
`Save` (which also flushes the staged delete).

## Testing

- Domain validation → unit tests
- Timezone conversion (resolve, DST gap/ambiguity, wall-clock round-trip) → unit tests
- Grid snapping arithmetic (floor/ceil, exact boundaries, cross-midnight) → unit tests, no DI
- SetAvailability / read use cases → integration tests, including outward snap
  and echo, wall-clock-frame snapping for a non-hour-offset submitter, rejection
  of a snapped range over 24 hours, and no re-alignment after an interval change
- HTTP endpoints → integration tests (Testcontainers PostgreSQL)

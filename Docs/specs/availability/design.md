# Availability Design

## Overview

Availability is a separate aggregate within the Scheduling bounded context. It
references Participant by ParticipantId. Availability is stored as canonical UTC
instants; wall-clock ↔ UTC conversion is centralized in a stateless
`TimezoneConverter` singleton shared with the Events feature.

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

`events.timezone` (the event's timezone option / fallback frame) and
`participants.timezone` (the participant's IANA timezone) are nullable text
columns; `participants.timezone` is captured at join/availability-submit and
`events.timezone` is set by the organizer's params toggle. `availabilities`
rows are `timestamp with time zone` and are always written as offset-0 UTC
instants.

## Validation

- Start must precede end.
- Duration must not exceed 24 hours (measured on the canonical UTC instants).
- The participant's `timezone` is required and must be a known IANA id
  (`invalid_timezone`); a range that is malformed, offset-bearing, ambiguous, or
  nonexistent in that timezone is rejected as `invalid_availability_range`.
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
converts each wall-clock range to UTC via `TimezoneConverter.ToUtc`, and returns
the ranges converted back via `ToWallClock`.

The replace semantics live in the use case, not the repository:
`SetAvailability` resolves the participant via `Event.FindParticipant`,
validates the guest id, then calls `AvailabilityRepository.DeleteForParticipant`
followed by `AvailabilityRepository.Save`. The repository exposes primitive
persistence ops only; atomicity comes from a single `SaveChanges` commit in
`Save` (which also flushes the staged delete).

## Testing

- Domain validation → unit tests
- Timezone conversion (resolve, DST gap/ambiguity, wall-clock round-trip) → unit tests
- SetAvailability / read use cases → integration tests
- HTTP endpoints → integration tests (Testcontainers PostgreSQL)

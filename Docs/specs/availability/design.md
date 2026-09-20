# Availability Design

## Overview

Availability is a separate aggregate within the Scheduling bounded context. It
references Participant by ParticipantId.

## Domain Model

    Participant 1 ──── 0..* Availability

    Availability
        - Start
        - End

Availability is a pure C# entity; the factory enforces the invariants.
Ownership flows Availability → Participant → Event, so an availability range
references only ParticipantId (no redundant EventId).

Participant resolution is done through the `Event` aggregate: `Event` exposes
`FindParticipant(ParticipantId)` and, for writes, is loaded with its
participants via `EventRepository.GetByPublicId` (not a separate read query).

## API

`PUT /events/{publicId}/participants/{participantId}/availability`

The PUT operation replaces all availability ranges for the participant.

The calling participant is resolved via
`GET /events/{publicId}/participant` (using the `X-Guest-Id` header).

Reads are served via `GET /events/{publicId}/availabilities` (all) and
`GET /events/{publicId}/participants/{participantId}/availability` (one).

## Persistence

`availabilities` table:
    - id
    - participant_id
    - start_utc
    - end_utc
    - created_at

The table enforces `end_utc > start_utc` and `end_utc - start_utc <= 24 hours`
as check constraints (mirrored by the factory validation).

`events` and `participants` each have a nullable `timezone` column (always null
today).

## Validation

- Start must precede end.
- Duration must not exceed 24 hours.
- Participant must belong to the specified event.
- The `X-Guest-Id` header must match the participant's guest id.

## Application Flow

Reads: API → `GetMyParticipant` / `GetEventAvailabilities` /
`GetParticipantAvailability` → `ReadEventService` + `ReadAvailabilityService`
(Dapper raw SQL) → PostgreSQL.

Writes: API → `SetAvailability` → `EventRepository.GetByPublicId` (loads the
`Event` aggregate with participants) + `AvailabilityRepository` (EF Core) →
PostgreSQL.

The replace semantics live in the use case, not the repository:
`SetAvailability` resolves the participant via `Event.FindParticipant`,
validates the guest id, then calls `AvailabilityRepository.DeleteForParticipant`
followed by `AvailabilityRepository.Save`. The repository exposes primitive
persistence ops only; atomicity comes from a single `SaveChanges` commit in
`Save` (which also flushes the staged delete).

## Testing

- Domain validation → unit tests
- SetAvailability / read use cases → integration tests
- HTTP endpoints → integration tests (Testcontainers PostgreSQL)

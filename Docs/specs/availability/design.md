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

## API

`PUT /events/{publicId}/participants/{participantId}/availability`

The PUT operation replaces all availability ranges for the participant.

Reads are served via `GET /events/{publicId}/availabilities` (all) and
`GET /events/{publicId}/participants/{participantId}/availability` (one).

## Persistence

`availabilities` table:
    - id
    - participant_id
    - start_utc
    - end_utc

`events` and `participants` each gain a nullable `timezone` column.

## Validation

- Start must precede end.
- Duration must not exceed 24 hours.
- Participant must belong to the specified event.
- The `X-Guest-Id` header must match the participant's guest id.

## Application Flow

API → SetAvailability / Get* → AvailabilityRepository / ReadAvailabilityService → PostgreSQL

Writes go through EF Core (`AvailabilityRepository`); reads go through Dapper +
raw SQL (`ReadAvailabilityService`).

## Testing

- Domain validation → unit tests
- SetAvailability / read use cases → integration tests
- HTTP endpoints → integration tests (Testcontainers PostgreSQL)

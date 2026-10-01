# Events Design

## Overview

Events is the root aggregate of the scheduling domain. `Event` owns its
`Participant` collection and the organizer is modeled as the first participant
referenced by `OrganizerParticipantId` rather than a separate entity. The
Availability feature references `Participant` by `ParticipantId`, not `Event`.
Event-level settings other than the organizer/passcode live in the `EventParams`
value object, and timezone conversion is centralized in a stateless
`TimezoneConverter` singleton (`Resolve`/`ToUtc`/`ToWallClock`) shared with the
availability paths.

## Domain Model

    Event 1 ──── 0..* Participant
    Event.OrganizerParticipantId → one of its Participants

    Event
        - Id (EventId)
        - PublicId
        - Name
        - OrganizerParticipantId
        - PasscodeHash (optional)
        - Params (EventParams; currently a Timezone: null = option off, IANA id = option on + fallback frame)

    Participant
        - ParticipantId
        - GuestId
        - Name
        - EventId
        - Timezone (IANA id; required at join)

`Event`, `Participant`, and the passcode value objects are pure C# with typed
ids (`EventId`, `ParticipantId`, `GuestId`, `PublicId`). `Event.Create` sets the
organizer's `EventId` and builds the event with the organizer as its only
participant. `Event.AddParticipant`/`RemoveParticipant` mutate the collection;
`FindParticipant` resolves a participant by id. `EventParams` groups event-level
settings (currently only `Timezone`) and `Event.SetParams` replaces it
wholesale; `Participant.SetTimezone` records the participant's timezone.

Passcodes are a small bounded sub-domain: `PasscodePolicy` fixes the alphabet
(`ABCDEFGHJKMNPQRSTUVWXYZ23456789`, ambiguous chars removed), length 6, PBKDF2
iterations 100,000, 16-byte salt, and 32-byte hash. `PasscodeFactory` returns a
`PasscodeCreated` holding both the plaintext `Passcode` (returned once) and the
`PasscodeHash` (persisted).

## API

- `POST /events` → 201 `{ publicId, isPasscodeProtected, passcode? }`
- `GET /events?guestId=` → 200 `{ events: [{ publicId, name, isOrganizer }] }`
- `GET /events/{publicId}` → 200 `{ publicId, name, isPasscodeProtected, timezone }`
- `PUT /events/{publicId}/params` → 200 `{ timezone }` (organizer-only, full-replace)
- `POST /events/{publicId}/join` → 201 `{ participantId, name }` (requires `timezone`)
- `POST /events/{publicId}/leave` → 204

All endpoints address an event by its shareable `publicId`; the internal UUID is
never exposed to clients. Request-shape validation (malformed uuid, blank
required fields) is inline `Results.BadRequest` in `EventsRestService`. Domain
rule failures throw application exceptions mapped in `Common/ExceptionHandler`:
`EventNotFoundException` → 404 `event_not_found`,
`InvalidPasscodeException` → 401 `invalid_passcode`,
`ParticipantAlreadyJoinedException` → 409 `participant_already_joined`,
`OrganizerCannotLeaveException` → 409 `organizer_cannot_leave`,
`InvalidTimezoneException` → 400 `invalid_timezone`,
`ForbiddenException` → 403 `forbidden`.

## Persistence

Writes use EF Core via `EventRepository`; reads use Dapper raw SQL via
`ReadEventService`.

`events` table:
    - id (uuid, pk)
    - public_id (text, unique index)
    - name (text)
    - organizer_participant_id (uuid)
    - timezone (text, nullable)
    - passcode_hash (bytea, nullable)
    - passcode_salt (bytea, nullable)

`participants` table:
    - id (uuid, pk)
    - guest_id (uuid)
    - name (text)
    - event_id (uuid, fk → events, cascade)
    - timezone (text, nullable)
    - unique index (event_id, guest_id)

Typed ids are stored as UUIDs via EF Core value converters; `PasscodeHash` is a
complex property mapped to the two bytea columns. The unique `(event_id,
guest_id)` index enforces single-join at the database level in addition to the
use case check.

`events.timezone` (nullable text) holds `EventParams.Timezone`, mapped as an EF
Core complex property — the column already existed, so no migration is needed.
`participants.timezone` (nullable text) holds the participant's IANA timezone,
captured at join and refreshed on availability submit.

## Validation

- `guestId` must be a valid UUID (create/join/leave/search).
- `organizerName` (create) and `participantName` (join) are required.
- `timezone` (join and params) must be a known IANA id; unknown ids are rejected
  as `invalid_timezone`.
- A participant must supply a timezone when joining.
- Only the organizer may change an event's params.
- A protected event requires a matching passcode to join.
- A guest may join an event only once.
- The organizer cannot leave their own event.

## Application Flow

Create: API → `CreateEvent` → `PublicIdGenerator` + `PasscodeFactory` →
`EventFactory` (via `ParticipantFactory`) → `EventRepository.Save` (EF Core).

Join: API → `JoinEvent` → `EventRepository.GetByPublicId` (loads participants) →
`PasscodeHasher.Verify` → `TimezoneConverter.Resolve` → `ParticipantFactory`
(with the timezone) → `Event.AddParticipant` → `EventRepository.Save`.

Update params: API → `UpdateEventParams` → `EventRepository.GetByPublicId` →
organizer check (`ForbiddenException`) → `TimezoneConverter.Resolve` (when
non-null) → `Event.SetParams` → `EventRepository.Save`.

Leave: API → `LeaveEvent` → `EventRepository.GetByPublicId` → organizer guard →
`Event.RemoveParticipant` → `EventRepository.Save`.

Reads: API → `ReadEventService` (Dapper raw SQL: `get_event_by_public_id.sql`,
`search_events.sql`) → PostgreSQL via a shared `NpgsqlDataSource`.

## Testing

- Domain invariants (event/participant factories, passcode hashing) → unit tests.
- Use cases and repositories → integration tests.
- HTTP endpoints → integration tests against Testcontainers PostgreSQL.

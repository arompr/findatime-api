# Initial Availability: Design

## Summary

Introduces the Availability aggregate and its four endpoints. Adds the
`availability_ranges` table and nullable `timezone` columns, new domain and
exception files, an EF Core write repository, and a Dapper read service.

## Changes

### Database (migration `AddAvailabilityRanges`)
- `availability_ranges` table: `id`, `event_id` (FK, cascade), `participant_id`
  (FK, cascade), `start_utc`, `end_utc`, `created_at`; CHECK `end_utc > start_utc`,
  CHECK `end_utc - start_utc <= interval '24 hours'`.
- Indexes on `event_id` and `(event_id, participant_id)`.
- `events.timezone` and `participants.timezone` nullable text columns.

### Domain
- `AvailabilityRange`, `AvailabilityRangeId`, `AvailabilityRangeFactory` (pure C#).
- `Event.Timezone` and `Participant.Timezone` (nullable string).

### Exceptions
- `ParticipantNotFoundException` (404 `participant_not_found`)
- `InvalidAvailabilityRangeException` (400 `invalid_availability_range`)
- `ForbiddenException` (403 `forbidden`)
All registered in `Common/ExceptionHandler`.

### Infra
- `DbContext`: `DbSet<AvailabilityRange>`, typed id and timezone column mappings.
- `AvailabilityRepository` (EF Core write side).
- `ReadAvailabilityService` (Dapper + raw SQL).
- SQL read queries: `get_participant_by_event_and_guest`,
  `get_event_availabilities`, `get_participant_availability` (embedded resources).

### Application
Use cases: `GetMyParticipant`, `GetEventAvailabilities`,
`GetParticipantAvailability`, `SetMyAvailability` (each with application DTOs).

### API
`AvailabilityRestService` with the four endpoints and inline request-shape
validation; response records map application DTOs.

### Frontend wiring
- API helpers `resolveParticipant`, `getEventAvailabilities`,
  `getParticipantAvailability`, `setMyAvailability`; TanStack Query hooks.
- Replace localStorage availability with the mutation; delete the local
  availability-storage and otherAvailability modules.

## Migration / Rollout

Apply migration locally and against staging; regenerate the TypeScript client
(`make typescript-client`) once all endpoints land.

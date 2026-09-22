# Events use public id: Design

## Summary

Move join and leave off the internal event UUID onto the shareable public id,
and stop returning the internal id from create. The internal id stays a pure
persistence concern.

## Changes

### API
- `POST /events/{publicId}/join`, `POST /events/{publicId}/leave` — route
  parameter changes from `Guid id` to `string publicId`.
- `CreateEventResponse` drops `EventId` → `(PublicId, IsPasscodeProtected, Passcode?)`.
- Join `Location` header becomes `/events/{publicId}/participants/{participantId}`.

### Application
- `JoinEvent.Execute(string publicId, ...)` and `LeaveEvent.Execute(string publicId, ...)`
  resolve the event via `EventRepository.GetByPublicId` (which already includes
  participants) instead of `GetById`.
- `CreateEvent.Execute` stops returning the internal id.

### Exceptions
- `InvalidPasscodeException`, `ParticipantAlreadyJoinedException`,
  `OrganizerCannotLeaveException` take `string publicId` instead of `Guid`.
- `EventNotFoundException` keeps only the `string publicId` overload (the `Guid`
  overload becomes unused and is removed).

### Infra
- No change. `GetByPublicId` already exists; `GetById` remains for infra-level
  round-trip tests.

## Migration / Rollout

No database migration. This is a breaking API change for any client still
relying on the internal event id in create/join/leave.

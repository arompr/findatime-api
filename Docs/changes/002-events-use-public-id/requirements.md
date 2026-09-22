# Events use public id: Requirements

## Goal

Make the shareable public id the only event identifier exposed to and accepted
from clients. Join and leave currently take the internal event UUID, which the
client should never need to know; create additionally returns that internal id
in its response.

## Scope

In scope:
- `POST /events/{publicId}/join` and `POST /events/{publicId}/leave` address an
  event by public id instead of internal UUID.
- Create no longer returns the internal event id; it returns only the public id,
  protected flag, and (optionally) the passcode.
- The join response `Location` header uses the public id.

Out of scope:
- Any change to search, retrieve, or the Availability endpoints (they already
  use the public id).
- Changing participant/guest identifiers.
- Database schema changes (no migration required).

## Acceptance Criteria

- [ ] `POST /events/{publicId}/join` joins by public id; unknown id → 404, wrong passcode → 401, duplicate join → 409.
- [ ] `POST /events/{publicId}/leave` leaves by public id; unknown id → 404, organizer leave → 409, non-member → 204.
- [ ] `POST /events` response no longer contains `eventId`.
- [ ] Join `Location` header is `/events/{publicId}/participants/{participantId}`.

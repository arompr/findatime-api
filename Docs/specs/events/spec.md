# Events

## Purpose

Events represent a schedulable meeting. An organizer creates an event and
becomes its first participant; other guests join (optionally supplying a
passcode) and are tracked by their guest id, which the API resolves from the
caller's guest token (see [Identity](../identity/spec.md)). The event is shared via a
short public id.

## Invariants

- An event has exactly one organizer, recorded as `OrganizerParticipantId`, and
  the organizer is always a participant of the event.
- A guest can be a participant of a given event at most once.
- A passcode-protected event is one whose `PasscodeHash` is non-null; the
  plaintext passcode is never stored.
- The organizer cannot leave their own event.
- The public id is unique across events.
- The timezone option is off by default and can only be changed by the organizer.
- A participant always carries the IANA timezone supplied when they joined.
- An event's interval is exactly one of 15, 30, or 60 minutes and defaults to
  30; only the organizer can change it.
- Changing an event's interval never re-aligns availability ranges that are
  already stored.
- Only the organizer can delete an event.
- Deleting an event is hard: the event, every participant, and every
  participant's availability ranges are removed, and nothing about them is kept.
  A deleted event cannot be recovered.
- Deleting succeeds however many participants are still joined, and a
  passcode-protected event is deleted without a passcode.

## State

An event has:

- Id (internal UUID)
- PublicId (shareable 12-character string)
- Name
- OrganizerParticipantId (a participant id)
- Timezone (optional; null = timezone option off, non-null IANA id = the frame of the slot grid and day window; null anchors them in UTC)
- IntervalMinutes (calendar step granularity; 15, 30, or 60 minutes; 30 on creation)
- PasscodeHash (optional; hash + salt)
- Participants (collection)

A participant has:

- ParticipantId (UUID)
- GuestId (UUID of the authenticated guest; never supplied in a request)
- Name
- EventId
- Timezone (IANA id; required at join)

## Behavior

### Create

Creating an event generates an internal id and a shareable public id, creates
the organizer as the first participant, and — when protection is enabled —
generates a passcode and stores only its salted hash. The plaintext passcode is
returned to the caller exactly once. The event's timezone option is off by
default (null) and its interval is 30 minutes; the create request carries
neither as a choice.

### Join

Joining verifies the passcode (if the event is protected), rejects a guest who
has already joined, and adds a participant. The participant's IANA timezone is
required; it is validated and recorded, even when the event's timezone option is
off.

### Leave

Leaving removes the calling guest from the event. Leaving an event the guest did
not join is a no-op; the organizer cannot leave.

### Update params

An event's params — the timezone option and the interval — can be replaced in
full by the organizer. The body always carries both: a non-null IANA timezone
anchors the event's slot grid and day window in that timezone, null anchors
them in UTC (legacy); the interval is the event's calendar step and must be 15, 30, or 60
minutes. The interval is validated before the timezone, and a value outside the
allowed set is rejected as `invalid_interval`. The operation is idempotent,
organizer-only, and never touches stored availability.

### Delete

The organizer deletes their own event by public id, identified by their guest
token. Deletion removes the event, every participant, and every
participant's availability ranges, and it succeeds however many participants are
still joined. No passcode is required, even for a protected event: organizer
identity is the only gate. The delete is hard — no soft-delete flag, no record,
no recovery — and it is not idempotent: a second delete of the same public id
finds no event.

### Query

An event can be looked up by public id, including its timezone option and its
interval. A guest can list every event they participate in, each flagged with
whether they are the organizer; the list carries no params. A deleted event is
simply gone: it is not found by public id and no longer appears in any guest's
list.

## API

Every endpoint except `GET /events/{publicId}` requires
`Authorization: Guest <token>` and answers 401 `unauthorized` without a valid
token. The caller is always the token's guest; no endpoint accepts a guest id.

### Create an event
`POST /events`

Body carries name, organizer name, a passcode-protected flag, and an optional
creator timezone. The caller becomes the organizer. Returns 201 with the public id, protected flag, and the passcode (null
when unprotected). Location is `/events/{publicId}`.

### Search events
`GET /events`

Returns every event the calling guest participates in, each with its public id, name,
and an organizer flag. Params (timezone, interval) are not part of the list
contract.

### Retrieve an event
`GET /events/{publicId}`

Returns the event's public id, name, passcode-protected flag, timezone (null
when the timezone option is off), and interval as `intervalMinutes`, or 404.

### Update event params
`PUT /events/{publicId}/params`

Organizer-only full-replace of the event's
params. Body `{ timezone, intervalMinutes }` — both fields are required:
`timezone` is an IANA id to enable/change the option or `null` to disable it, and
`intervalMinutes` is one of 15, 30, 60. Returns 200 with the resulting
`{ timezone, intervalMinutes }`; 400 `invalid_interval` for an out-of-set
interval, 400 `invalid_timezone` for an unknown id, 400 `intervalMinutes is
required` for a missing interval,
403 `forbidden` for a non-organizer, 404 if the event is missing.

### Join an event
`POST /events/{publicId}/join`

`{publicId}` is the shareable public id. Body carries the passcode (optional),
participant name, and the participant's IANA timezone
(required). Returns 201 with the participant id and name; 404 if the event is
missing, 401 if the passcode is wrong, 409 if the guest already joined, 400
`timezone is required` if the timezone is blank, 400 `invalid_timezone` if it is
not a known IANA id, 429 `rate_limited` when the caller's IP exceeds the
per-minute join limit (`RateLimiting:JoinsPerMinute`).

### Leave an event
`POST /events/{publicId}/leave`

No body; the caller leaves. Returns 204; 404 if the event is missing, 409
if the organizer attempts to leave.

### Delete an event
`DELETE /events/{publicId}`

Organizer-only; there is no request body. Returns 204 with no body, deleting the event together with its
participants and their availability ranges. 403 `forbidden`
for a non-organizer, 404 `event_not_found` when the event is missing or already
deleted.

Every read of a deleted event misses it: `GET /events/{publicId}` and
`GET /events/{publicId}/participant` return 404 `event_not_found`, both
availability reads (`GET /events/{publicId}/availabilities` and
`GET /events/{publicId}/participants/{participantId}/availability`) return 404
`event_not_found`, and the event no longer appears in `GET /events`.

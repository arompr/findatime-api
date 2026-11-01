# Events

## Purpose

Events represent a schedulable meeting. An organizer creates an event and
becomes its first participant; other guests join (optionally supplying a
passcode) and are tracked by a per-browser guest id. The event is shared via a
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

## State

An event has:

- Id (internal UUID)
- PublicId (shareable 12-character string)
- Name
- OrganizerParticipantId (a participant id)
- Timezone (optional; null = timezone option off, non-null IANA id = option on and the fallback display frame)
- IntervalMinutes (calendar step granularity; 15, 30, or 60 minutes; 30 on creation)
- PasscodeHash (optional; hash + salt)
- Participants (collection)

A participant has:

- ParticipantId (UUID)
- GuestId (UUID supplied by the client)
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
enables timezone-aware display and sets the fallback frame, null disables it
(legacy); the interval is the event's calendar step and must be 15, 30, or 60
minutes. The interval is validated before the timezone, and a value outside the
allowed set is rejected as `invalid_interval`. The operation is idempotent,
organizer-only, and never touches stored availability.

### Query

An event can be looked up by public id, including its timezone option and its
interval. A guest can list every event they participate in, each flagged with
whether they are the organizer; the list carries no params.

## API

### Create an event
`POST /events`

Body carries name, guest id (uuid), organizer name, and a passcode-protected
flag. Returns 201 with the public id, protected flag, and the passcode (null
when unprotected). Location is `/events/{publicId}`.

### Search events
`GET /events?guestId={guestId}`

Returns every event the guest participates in, each with its public id, name,
and an organizer flag. Params (timezone, interval) are not part of the list
contract.

### Retrieve an event
`GET /events/{publicId}`

Returns the event's public id, name, passcode-protected flag, timezone (null
when the timezone option is off), and interval as `intervalMinutes`, or 404.

### Update event params
`PUT /events/{publicId}/params`

Organizer-only (matched via the `X-Guest-Id` header) full-replace of the event's
params. Body `{ timezone, intervalMinutes }` — both fields are required:
`timezone` is an IANA id to enable/change the option or `null` to disable it, and
`intervalMinutes` is one of 15, 30, 60. Returns 200 with the resulting
`{ timezone, intervalMinutes }`; 400 `invalid_interval` for an out-of-set
interval, 400 `invalid_timezone` for an unknown id, 400 `intervalMinutes is
required` for a missing interval, 400 for a missing/malformed guest id header,
403 `forbidden` for a non-organizer, 404 if the event is missing.

### Join an event
`POST /events/{publicId}/join`

`{publicId}` is the shareable public id. Body carries the passcode (optional),
guest id (uuid), participant name, and the participant's IANA timezone
(required). Returns 201 with the participant id and name; 404 if the event is
missing, 401 if the passcode is wrong, 409 if the guest already joined, 400
`timezone is required` if the timezone is blank, 400 `invalid_timezone` if it is
not a known IANA id.

### Leave an event
`POST /events/{publicId}/leave`

Body carries the guest id (uuid). Returns 204; 404 if the event is missing, 409
if the organizer attempts to leave.

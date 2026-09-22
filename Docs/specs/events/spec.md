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

## State

An event has:

- Id (internal UUID)
- PublicId (shareable 12-character string)
- Name
- OrganizerParticipantId (a participant id)
- Timezone (optional, always null today)
- PasscodeHash (optional; hash + salt)
- Participants (collection)

A participant has:

- ParticipantId (UUID)
- GuestId (UUID supplied by the client)
- Name
- EventId
- Timezone (optional, always null today)

## Behavior

### Create

Creating an event generates an internal id and a shareable public id, creates
the organizer as the first participant, and — when protection is enabled —
generates a passcode and stores only its salted hash. The plaintext passcode is
returned to the caller exactly once.

### Join

Joining verifies the passcode (if the event is protected), rejects a guest who
has already joined, and adds a participant.

### Leave

Leaving removes the calling guest from the event. Leaving an event the guest did
not join is a no-op; the organizer cannot leave.

### Query

An event can be looked up by public id. A guest can list every event they
participate in, each flagged with whether they are the organizer.

## API

### Create an event
`POST /events`

Body carries name, guest id (uuid), organizer name, and a passcode-protected
flag. Returns 201 with the public id, protected flag, and the passcode (null
when unprotected). Location is `/events/{publicId}`.

### Search events
`GET /events?guestId={guestId}`

Returns every event the guest participates in, each with its public id, name,
and an organizer flag.

### Retrieve an event
`GET /events/{publicId}`

Returns the event's public id, name, and passcode-protected flag, or 404.

### Join an event
`POST /events/{publicId}/join`

`{publicId}` is the shareable public id. Body carries the passcode (optional),
guest id (uuid), and participant name. Returns 201 with the participant id and
name; 404 if the event is missing, 401 if the passcode is wrong, 409 if the
guest already joined.

### Leave an event
`POST /events/{publicId}/leave`

Body carries the guest id (uuid). Returns 204; 404 if the event is missing, 409
if the organizer attempts to leave.

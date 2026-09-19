# Availability

## Purpose

Availability represents the time ranges during which a participant is available
for an event. An organizer reads availability to choose a slot that works for
everyone.

## Invariants

- Availability belongs to exactly one participant.
- A participant can have zero or more availability ranges.
- Ranges must have a start before their end.
- Ranges must not exceed 24 hours in duration.
- Clearing a participant's availability is done by replacing it with no ranges.

## State

An availability range has:

- Start (UTC instant)
- End (UTC instant)

A participant has an optional timezone (always null today).

## Behavior

### Replace

Replacing a participant's availability removes all existing ranges and stores
the supplied ranges in a single operation.

### Query

Availability can be retrieved for all participants of an event, or for a single
participant.

## API

### Resolve the calling participant
`GET /events/{publicId}/participant`

Returns the participant id and name for the calling guest. Requires an
`X-Guest-Id` header.

### List event availability
`GET /events/{publicId}/availabilities`

Returns every participant and their ranges for the event.

### Read one participant's availability
`GET /events/{publicId}/participants/{participantId}/availability`

Returns a single participant's ranges.

### Replace participant availability
`PUT /events/{publicId}/participants/{participantId}/availability`

The request represents the participant's complete desired availability.
Requires an `X-Guest-Id` header that must match the participant's guest id.

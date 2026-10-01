# Availability

## Purpose

Availability represents the time ranges during which a participant is available
for an event. An organizer reads availability to choose a slot that works for
everyone. Ranges are stored as canonical UTC instants and, when the event's
timezone option is on, rendered as wall-clock times in the viewer's timezone.

## Invariants

- Availability belongs to exactly one participant.
- A participant can have zero or more availability ranges.
- Ranges must have a start before their end.
- Ranges must not exceed 24 hours in duration.
- Each range is stored as a canonical UTC start/end pair, independent of the submitter's timezone.
- Ranges are submitted as wall-clock times in the participant's timezone and read as wall-clock times in the resolved viewer frame.
- Clearing a participant's availability is done by replacing it with no ranges.

## State

An availability range has:

- Start (canonical UTC instant)
- End (canonical UTC instant)

A participant has a timezone (IANA id), captured when they join or submit
availability.

## Behavior

### Replace

Replacing a participant's availability removes all existing ranges and stores
the supplied ranges. The request supplies the participant's IANA timezone and
the ranges as wall-clock local times; each range is converted to a canonical UTC
instant before storage. The response returns the ranges converted back to the
participant's wall-clock times plus the timezone label.

### Query

Availability can be retrieved for all participants of an event, or for a single
participant. The request supplies an optional viewer timezone; when the event's
timezone option is on, each stored UTC range is rendered as wall-clock in the
viewer's frame (viewer timezone → event timezone → UTC) along with the label.
When the option is off, times are rendered in UTC with the label `UTC` (legacy,
no conversion).

## API

### Resolve the calling participant
`GET /events/{publicId}/participant`

Returns the participant id and name for the calling guest. Requires an
`X-Guest-Id` header.

### List event availability
`QUERY /events/{publicId}/availabilities`

Body carries an optional `{ timezone }` (the viewer's IANA frame; an unknown id
is rejected with 400 `invalid_timezone`). Returns the resolved timezone label
and every participant with their ranges rendered as wall-clock times in that
frame. Because it reads with a body, the operation uses the `QUERY` verb on its
original path.

### Read one participant's availability
`QUERY /events/{publicId}/participants/{participantId}/availability`

Body carries an optional `{ timezone }` viewer frame, validated and applied as
above. Returns the participant's name, the resolved timezone label, and their
ranges as wall-clock times.

### Replace participant availability
`PUT /events/{publicId}/participants/{participantId}/availability`

The request represents the participant's complete desired availability. Body
carries the participant's IANA `timezone` and `ranges` as `{ start, end }`
wall-clock strings without offset; 400 `timezone is required` when blank and 400
`invalid_timezone` for an unknown id. Returns the participant id, name, timezone
label, and the ranges as wall-clock times. Requires an `X-Guest-Id` header that
must match the participant's guest id.

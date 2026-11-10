# Availability

## Purpose

Availability represents the time ranges during which a participant is available
for an event. An organizer reads availability to choose a slot that works for
everyone. Ranges are exchanged and stored as UTC instants. The server snaps them
to the event's slot grid in the event's timezone; clients convert instants to
local time only for display.

## Invariants

- Availability belongs to exactly one participant.
- A participant can have zero or more availability ranges.
- Ranges must have a start before their end.
- Ranges must not exceed 24 hours in duration.
- Each range is stored as a canonical UTC start/end pair.
- Ranges are submitted and returned as ISO 8601 instants with an explicit UTC
  offset. Responses always use offset 0 (`+00:00`).
- Clearing a participant's availability is done by replacing it with no ranges.
- Submitted bounds are snapped outward onto the event's interval grid (15, 30, or
  60 minutes) in the event's timezone (UTC when the event has none): the start
  moves down to the previous grid boundary, the end up to the next one.
- Stored ranges are never rewritten when the event's interval changes; reads
  project them inward onto the current interval grid instead (see Behavior →
  Projection onto the current interval).
- Writing availability never changes the participant's timezone. It is recorded
  once, at join.

## State

An availability range has:

- Start (canonical UTC instant)
- End (canonical UTC instant)

A participant has a timezone (IANA id), captured when they join. Availability
does not use it.

The event an availability belongs to carries an interval (15, 30, or 60 minutes,
30 by default) and an optional timezone. Together they define the slot grid:
boundaries every `interval` minutes from midnight in the event's timezone, or
from midnight UTC for legacy events without a timezone. Submitted bounds are
snapped outward to that grid and reads project stored ranges inward onto it.

## Behavior

### Replace

Replacing a participant's availability removes all existing ranges and stores
the supplied ranges. The request supplies only the ranges, as instants. Each
range is snapped outward onto the event's grid in the event's timezone: the
start is floored to the previous boundary and the end ceiled to the next one. A
bound already on the grid is unchanged. The snapped instants, normalized to UTC,
are what is stored, and the response returns them, so the caller sees exactly
what was saved.

Snapping happens in the event's frame, the same frame reads project in, so a
saved range is never trimmed or dropped by the next read. A viewer in a
half-hour-offset zone on a 60-minute New York event who submits 19:00–20:00 IST
(13:30–14:30Z) gets 09:00–11:00 New York time (13:00–15:00Z) in summer: outward
snapping only widens.

The end-after-start check runs on the submitted range, and the factory's
end-after-start and 24-hour checks run again on the snapped range. Outward
rounding can push a range that sat at the 24-hour limit past it; such a range
is rejected as `invalid_availability_range`, never clamped. Instants have no DST
gaps or ambiguities, so a write never fails for DST reasons. Snapping resolves a
boundary that falls in a gap or on a repeated hour outward.

### Projection onto the current interval

Reads express stored ranges on the grid of the event's current interval. A
participant's ranges are merged into a union first (overlapping and touching
ranges collapse into one block), and each block is projected inward: the start
moves up to the next grid boundary and the end down to the previous one. A block
that contains no full interval is omitted from the result; it is never widened
or clamped. Bounds already on the grid are unchanged.

The grid is anchored at midnight in the event's timezone, or at midnight UTC
when the event has no timezone. Reads have no viewer input, so every reader gets
the same instants.

Projection is a read-time view: stored rows are never modified when the interval
changes, so switching back to a finer interval restores the earlier output. A
participant whose ranges all project away is still listed with an empty range
list. Because reads must not fail, a projected boundary that falls in a DST gap
advances to the end of the gap, and a repeated boundary resolves to the inward
interpretation.

### Query

Availability can be retrieved for all participants of an event, or for a single
participant. Reads take no body and no timezone. They return each participant's
ranges projected onto the event's current interval as UTC instants. The
responses carry no interval or timezone field.

## API

Every availability endpoint requires `Authorization: Guest <token>` (401
`unauthorized` otherwise), and the caller must be a participant of the event:
reads by a guest who has not joined answer 403 `forbidden` (404
`event_not_found` still wins for an unknown event).

### Resolve the calling participant
`GET /events/{publicId}/participant`

Returns the participant id and name for the calling guest; 404
`participant_not_found` when the guest has not joined.

### List event availability
`GET /events/{publicId}/availabilities`

Returns `{ participants: [{ participantId, name, ranges: [{ start, end }] }] }`.
Every participant is listed, ordered by name, with their ranges projected onto
the event's current interval as UTC instants. Participants whose ranges all
project away are listed with an empty range list. 404 when the event does not
exist.

### Read one participant's availability
`GET /events/{publicId}/participants/{participantId}/availability`

Returns `{ participantId, name, ranges }`, with ranges projected as above. 400
when `participantId` is not a uuid; 404 when the event or participant does not
exist or the participant belongs to another event.

### Replace participant availability
`PUT /events/{publicId}/participants/{participantId}/availability`

The request represents the participant's complete desired availability. The body
is `{ ranges: [{ start, end }] }`, where each bound is an ISO 8601 instant with
an explicit offset (`Z` or `±hh:mm`). Each range is snapped outward onto the
event's interval grid in the event's timezone. The response is
`{ participantId, name, ranges }` with the stored, snapped ranges as UTC
instants. Reads later project them onto the interval current at read time.

400 `invalid_availability_range` when a bound is malformed or has no offset
(e.g. `2026-11-03T14:00:00`), when a range's end is not after its start, or when
a snapped range exceeds 24 hours. The caller must be that participant (403
otherwise).

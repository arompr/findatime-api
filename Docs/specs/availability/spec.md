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
- Submitted wall-clock bounds are snapped outward onto the event's interval grid (15, 30, or 60 minutes) in the submitter's timezone before UTC conversion: the start moves down to the previous grid boundary, the end up to the next one.
- Stored ranges are never rewritten when the event's interval changes; reads project them inward onto the current interval grid instead (see Behavior → Projection onto the current interval).

## State

An availability range has:

- Start (canonical UTC instant)
- End (canonical UTC instant)

A participant has a timezone (IANA id), captured when they join or submit
availability.

The event an availability belongs to carries an interval — 15, 30, or 60
minutes, 30 by default — which is the calendar step: submitted bounds are
snapped outward to it and reads project stored ranges inward onto it. It is an
event-level setting, not a per-participant one.

## Behavior

### Replace

Replacing a participant's availability removes all existing ranges and stores
the supplied ranges. The request supplies the participant's IANA timezone and
the ranges as wall-clock local times. Each bound is first snapped onto the
event's interval grid in the submitter's timezone — the start floored down to the
previous grid boundary, the end ceiled up to the next one — and the snapped
wall-clock values are then converted to canonical UTC instants before storage. A
bound already on the grid is unchanged. The snapped values are what is stored,
and the response returns them converted back to the participant's wall-clock
times plus the timezone label, so the caller sees exactly what was saved.

Snapping happens in the submitter's wall-clock frame, not in the UTC frame: the
wall clock is the frame the participant actually saw, and a submitter in a
non-hour-offset timezone (e.g. `+05:30`) can keep their own grid even though the
resulting UTC instants fall off it.

The end-after-start and 24-hour-maximum invariants are applied to the snapped
values. Outward rounding only widens a range, so it can never turn a valid range
into a zero-duration one, but it can push a range that sat at the 24-hour limit
past it; such a range is rejected as `invalid_availability_range`, never clamped
back to the grid or to the limit. The DST gap/ambiguous rejection applies to the
snapped bounds, exactly as it does to any other wall-clock bound.

### Projection onto the current interval

Reads express stored ranges on the grid of the event's current interval. A
participant's ranges are merged into a union first (overlapping and touching
ranges collapse into one block), and each block is projected inward: the start
moves up to the next grid boundary and the end down to the previous one. A block
that contains no full interval is omitted from the result; it is never widened
or clamped. Bounds already on the grid are unchanged.

The grid is anchored at midnight in the event's timezone, or at midnight UTC
when the timezone option is off. The viewer's timezone affects only the
wall-clock rendering, never the projected instants, so every reader sees the
same slots.

Projection is a read-time view: stored rows are never modified when the interval
changes, so switching back to a finer interval restores the earlier output. A
participant whose ranges all project away is still listed with an empty range
list. Because reads must not fail, a projected boundary that falls in a DST gap
advances to the end of the gap, and a repeated boundary resolves to the inward
interpretation.

### Query

Availability can be retrieved for all participants of an event, or for a single
participant. The request supplies an optional viewer timezone; when the event's
timezone option is on, each stored UTC range is rendered as wall-clock in the
viewer's frame (viewer timezone → event timezone → UTC) along with the label.
When the option is off, times are rendered in UTC with the label `UTC` (legacy,
no conversion). Reads project the stored ranges onto the event's current
interval (see Projection onto the current interval); the responses carry no
interval field.

## API

### Resolve the calling participant
`GET /events/{publicId}/participant`

Returns the participant id and name for the calling guest. Requires an
`X-Guest-Id` header.

### List event availability
`QUERY /events/{publicId}/availabilities`

Body carries an optional `{ timezone }` (the viewer's IANA frame; an unknown id
is rejected with 400 `invalid_timezone`). Returns the resolved timezone label
and every participant with their ranges projected onto the event's current
interval and rendered as wall-clock times in that frame. Participants whose
ranges all project away are listed with an empty range list. Because it reads
with a body, the operation uses the `QUERY` verb on its original path.

### Read one participant's availability
`QUERY /events/{publicId}/participants/{participantId}/availability`

Body carries an optional `{ timezone }` viewer frame, validated and applied as
above. Returns the participant's name, the resolved timezone label, and their
ranges projected onto the event's current interval and rendered as wall-clock
times.

### Replace participant availability
`PUT /events/{publicId}/participants/{participantId}/availability`

The request represents the participant's complete desired availability. Body
carries the participant's IANA `timezone` and `ranges` as `{ start, end }`
wall-clock strings without offset; 400 `timezone is required` when blank and 400
`invalid_timezone` for an unknown id. Each bound is snapped onto the event's
interval grid in the supplied timezone before conversion, and the ranges returned
are the snapped ones (reads later project them onto the interval current at read
time); 400 `invalid_availability_range` for a malformed or
offset-bearing bound, a wall-clock bound that is ambiguous or nonexistent in the
supplied timezone, or a snapped range whose end is not after its start or whose
duration exceeds 24 hours. Returns the participant id, name, timezone label, and
the ranges as wall-clock times. Requires an `X-Guest-Id` header that must match
the participant's guest id.

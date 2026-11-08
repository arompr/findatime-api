# 006-availability-interval-projection: Proposal

## Goal

Make availability reads reflect the event's **current** interval. Today the
interval is applied once, when a participant submits, and never again: a range
entered on a 30-minute grid keeps its half-hour bounds after the organizer
switches the event to 60-minute slots. The organizer then sees availability
that does not line up with the slots they can actually pick — `08:30–09:30`
covers no full hour, and `08:30–10:00` is shown as covering more than the
`09:00–10:00` slot it really supports.

The change projects stored availability inward onto the current interval at
read time: the start ceils to the next grid boundary, the end floors to the
previous one, and a range that contains no full interval contributes nothing.
Storage is untouched, so switching back to a finer interval restores the
original detail.

## Scope

In scope:

- A shared, stateless projection helper that merges a participant's stored
  ranges and projects the union inward onto an interval grid anchored in the
  event's timezone.
- Inward grid primitives in `Common/Interval` alongside the existing outward
  ones.
- Both availability read use cases (`GetEventAvailabilities`,
  `GetParticipantAvailability`) run stored ranges through the projection.
- `get_event_availabilities.sql` and `EventAvailabilityDto` carry the event's
  `interval_minutes`.
- Unit tests for the projection (alignment, short ranges, unions, frames, DST)
  and integration tests for both read endpoints across an interval change.
- `availability.hurl` entries proving the projection at the wire level.
- Feature-spec deltas folded into `Docs/specs/availability/`.

Out of scope:

- Any change to the write path: `SetAvailability` keeps snapping outward, and
  the PUT response keeps echoing the stored snapped values.
- Any schema change or data rewrite. No migration; stored rows are never
  re-aligned when the interval changes.
- Any change to the availability read response shape (no `intervalMinutes`
  field, no new endpoint).
- A slot-picking feature. The projection only reports which parts of the stored
  availability fit the current interval; it does not choose a slot or compute
  overlaps between participants.
- Fixing the accepted write-side widening of ranges shorter than the interval
  (e.g. `09:05–09:50` submitted while the interval is 60).
- The TypeScript client: the read endpoints are absent from it and response
  shapes do not change, so no regeneration is needed.

## Rationale

- **The organizer's question is "which of my slots do people cover?", not "what
  exactly did they type?".** Projecting at read time answers that question with
  the current interval and leaves the participant's submitted fact intact.
- **Read-time projection is non-destructive.** Re-aligning rows when the
  interval changes would erase half-hour detail the moment the organizer
  previews 60-minute slots; flipping back to 30 could not restore it.
- **Inward, not outward.** Outward rounding at read time would invent
  availability (`08:30–09:30` → `08:00–10:00`). Inward rounding can only remove
  time, which is the safe direction for a scheduling decision.
- **Union first.** Treating each stored row in isolation would drop adjacent
  half-hour ranges that together cover a full hour.
- **One shared helper.** Both read endpoints must agree, and the arithmetic is
  pure enough to unit-test without a database or DI.

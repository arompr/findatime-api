# 008-availability-utc-instants: Spec

The **what**. Testable requirements and scenarios only.

## Requirements

### REQ-001: Instants on the wire
WHEN a client writes or reads availability THEN THE SYSTEM SHALL exchange ranges
as `{ start, end }` ISO 8601 instants with an explicit offset, SHALL answer with
offset 0 (`+00:00`), and SHALL carry no `timezone` field in any availability
request or response.

### REQ-002: Reads are GET
WHEN a client reads availability THEN THE SYSTEM SHALL serve
`GET /events/{publicId}/availabilities` →
`{ participants: [{ participantId, name, ranges }] }` and
`GET /events/{publicId}/participants/{participantId}/availability` →
`{ participantId, name, ranges }`, with no request body, and SHALL NOT expose a
`QUERY` operation.

### REQ-003: Write snaps outward in the event frame
WHEN a participant sends `PUT …/availability` with `{ ranges }` THEN THE SYSTEM
SHALL snap each range outward (start floored, end ceiled) onto the event's
interval grid anchored at midnight in the event's timezone, or UTC when the
event has none, store the snapped instants, and return them as
`{ participantId, name, ranges }`.

### REQ-004: Offset required
WHEN a range bound has no explicit offset, or is not a valid ISO 8601 datetime,
THEN THE SYSTEM SHALL respond 400 `invalid_availability_range` and store
nothing.

### REQ-005: Range validity
WHEN a range's end is not after its start, or a snapped range exceeds 24 hours,
THEN THE SYSTEM SHALL respond 400 `invalid_availability_range` and store nothing.

### REQ-006: Participant timezone set at join only
WHEN a participant saves availability THEN THE SYSTEM SHALL leave the
participant's recorded timezone unchanged.

### REQ-007: Reads unchanged apart from the frame
WHEN availability is read THEN THE SYSTEM SHALL project stored ranges inward
onto the event's current grid in the event's timezone (UTC when none) exactly as
before, and SHALL return the same instants to every viewer.

## Scenarios

### SC-001: Aligned instants pass through
GIVEN a New York event with 30-min slots
WHEN the participant saves `2026-11-03T14:00:00Z`–`2026-11-03T18:00:00Z`
THEN the response, the stored row and the next read are exactly
`14:00:00+00:00`–`18:00:00+00:00`.

### SC-002: Half-hour-offset viewer survives a save and read
GIVEN a New York event with 60-min slots and a participant who joined from
`Asia/Kolkata`
WHEN they save `2026-09-19T13:30:00Z`–`14:30:00Z` (19:00–20:00 IST, 09:30–10:30
EDT)
THEN the stored and returned range is **`13:00:00Z`–`15:00:00Z`** (09:00–11:00
EDT), and both reads return that same range, never empty.

Note: outward snapping widens a misaligned range on both ends, so the result is
the two-slot 13:00–15:00Z, not one of the single slots 13:00–14:00Z or
14:00–15:00Z.

### SC-003: Legacy event anchors to UTC
GIVEN an event without a timezone and 60-min slots
WHEN a participant saves `13:30Z`–`14:15Z`
THEN the stored range is `13:00Z`–`15:00Z`; and a stored `03:30Z`–`05:30Z` reads
back as `04:00Z`–`05:00Z`.

### SC-004: Participant timezone unchanged
GIVEN a participant who joined a New York event from `America/Vancouver`
WHEN they save availability
THEN their stored timezone is still `America/Vancouver`.

### SC-005: Offset-less bound rejected
WHEN a PUT carries `"start": "2026-11-03T14:00:00"` (no offset)
THEN the response is 400 with `error: "invalid_availability_range"`.

### SC-006: Non-UTC offsets normalized
WHEN a PUT carries `2026-09-21T11:30:00+02:00`–`12:30:00+02:00`
THEN the response range is `09:30:00+00:00`–`10:30:00+00:00`.

### SC-007: DST-spanning range snaps outward
GIVEN a New York event with 60-min slots
WHEN a range covers the Nov 1, 2026 fall-back hour (`05:20Z`–`06:40Z`, 01:20 EDT
to 01:40 EST)
THEN it snaps to `05:00Z`–`07:00Z`, never shrinking.

### SC-008: Contract check against the running API
WHEN `GET /events/IKQN3ySKQJFl/availabilities` is called
THEN it returns UTC instants with no `timezone` field, and Test 1's Nov 3 range
is `2026-11-03T14:00:00+00:00`–`18:00:00+00:00`.

## Acceptance criteria

- [ ] No `timezone` property on any availability request or response schema.
- [ ] Both availability reads are `GET` in the OpenAPI document; range
      properties are `format: date-time`.
- [ ] Writes snap in the event frame; SC-001 to SC-007 hold.
- [ ] A save never changes `participants.timezone`.
- [ ] The regenerated TypeScript client exposes `getEventAvailabilities` and
      `getParticipantAvailability` from the generated SDK, with no hand-written
      `query.ts`.

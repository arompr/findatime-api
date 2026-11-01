# 004-events-time-intervals: Spec

The **what**. Testable requirements and scenarios only — no implementation,
no architecture, no code.

## Requirements

### REQ-001: Interval is a closed-set event-level param
WHEN an event carries params THEN THE SYSTEM SHALL hold an interval value that
is exactly one of `15`, `30`, or `60` minutes.

### REQ-002: Interval defaults to 30 on creation
WHEN an event is created via `POST /events` THEN THE SYSTEM SHALL set its
interval to `30` minutes, without requiring any interval field in the create
request.

### REQ-003: Existing events land on interval 30
WHEN an event that existed before this change is read THEN THE SYSTEM SHALL
report its interval as `30` minutes; existing events SHALL NOT have their
stored availability ranges re-snapped, re-validated, or rewritten.

### REQ-004: Interval is returned on event read
WHEN `GET /events/{publicId}` returns an event THEN THE SYSTEM SHALL include
`intervalMinutes` in the response alongside the existing fields (`publicId`,
`name`, `isPasscodeProtected`, `timezone`).

### REQ-005: Params update is a full replace that requires both fields
WHEN the organizer updates params via `PUT /events/{publicId}/params` THEN THE
SYSTEM SHALL require the body to carry both `timezone` and `intervalMinutes`;
the operation replaces the whole params value object.

### REQ-006: Invalid interval value is rejected with a dedicated code
WHEN the params body carries an `intervalMinutes` outside the allowed set
(`15`/`30`/`60`) THEN THE SYSTEM SHALL reject the request with HTTP 400 and the
machine-readable error code `invalid_interval`.

### REQ-007: Missing intervalMinutes is a request-shape error
WHEN `intervalMinutes` is absent from the params body THEN THE SYSTEM SHALL
reject the request with HTTP 400 (request-shape, missing required field),
consistent with how other missing required fields are handled.

### REQ-008: Params update keeps existing failure behavior
WHEN the params body carries an unknown IANA timezone THEN THE SYSTEM SHALL
reject with 400 `invalid_timezone` (unchanged).
WHEN the caller is not the organizer THEN THE SYSTEM SHALL reject with 403
`forbidden`.
WHEN the event does not exist THEN THE SYSTEM SHALL reject with 404
`event_not_found`.

### REQ-009: Params response echoes both params
WHEN a params update succeeds THEN THE SYSTEM SHALL return 200 with a response
carrying both `timezone` and `intervalMinutes` reflecting the stored values.

### REQ-010: Interval is exposed only on the params and event-read contracts
WHEN the availability read endpoints or the event search/list endpoint return a
response THEN THE SYSTEM SHALL NOT include `intervalMinutes`; those response
shapes SHALL remain unchanged.

### REQ-011: Submitted ranges are snapped to the interval grid before UTC conversion
WHEN a participant submits availability ranges THEN THE SYSTEM SHALL snap each
range's `start` down and `end` up onto the event's interval grid, computed in
the submitter's wall-clock timezone, BEFORE the existing wall-clock → UTC
conversion is applied.

### REQ-012: Aligned bounds are stored as-is
WHEN a submitted bound already lies exactly on the interval grid in the
submitter's wall-clock timezone THEN THE SYSTEM SHALL leave it unchanged.

### REQ-013: Snapped values are stored and echoed
WHEN a range is snapped THEN THE SYSTEM SHALL store the resulting UTC instants
as the range, and SHALL return the snapped wall-clock values in the response so
the caller sees exactly what was stored.

### REQ-014: Invariants apply to snapped values, never clamped
WHEN the snapped range violates `end > start` or the 24-hour maximum THEN THE
SYSTEM SHALL reject with 400 `invalid_availability_range`; the system SHALL
never silently clamp a bound back to the grid or to a limit.

### REQ-015: Snapping cannot create a zero-duration range
WHEN a range is snapped THEN THE SYSTEM SHALL preserve the pre-existing
guarantee that `end > start`; because snapping widens outward (`start` down,
`end` up), a range that was valid before snapping SHALL remain valid after.

### REQ-016: DST gap/ambiguous wall-clock handling is applied to snapped values
WHEN a snapped wall-clock bound falls in a DST gap or ambiguous period of the
submitter's timezone THEN THE SYSTEM SHALL apply the existing gap/ambiguous
rejection behavior (400 `invalid_availability_range`) to the snapped value,
exactly as it does today.

### REQ-017: Non-hour-offset timezones are supported
WHEN an event uses a `60`-minute interval and the submitter's timezone has a
non-hour offset (e.g. `+05:30`) THEN THE SYSTEM SHALL snap on the submitter's
wall-clock grid and SHALL allow the stored UTC instant to fall off that grid.

### REQ-018: Outward snapping may push a 24-hour range over the limit
WHEN a range is exactly 24 hours before snapping and outward rounding extends
it past the 24-hour maximum THEN THE SYSTEM SHALL reject with 400
`invalid_availability_range`.

### REQ-019: Interval changes do not retroactively re-align stored ranges
WHEN the organizer changes the event's interval THEN THE SYSTEM SHALL leave all
stored availability ranges unchanged, and availability reads SHALL continue to
return the stored ranges verbatim regardless of the current interval.

### REQ-020: Frontend handoff document
WHEN this change is delivered THEN THE SYSTEM SHALL include a frontend handoff
document at `Docs/changes/004-events-time-intervals/frontend-handoff.md` whose
content satisfies the requirements listed in the "Frontend Handoff Document"
section below.

## Scenarios

### SC-001: Fresh event defaults to 30
GIVEN an event created with no interval field WHEN the organizer reads it via
`GET /events/{publicId}` THEN the response SHALL include `"intervalMinutes": 30`.

### SC-002: Organizer changes interval to 15
GIVEN an existing event WHEN the organizer sends
`PUT /events/{publicId}/params` with body
`{ "timezone": "Europe/Berlin", "intervalMinutes": 15 }` THEN the system SHALL
return 200 and the response SHALL be
`{ "timezone": "Europe/Berlin", "intervalMinutes": 15 }`.

### SC-003: Organizer changes interval to 60
GIVEN an existing event WHEN the organizer sends
`PUT /events/{publicId}/params` with `intervalMinutes` `60` (and a valid or
null `timezone`) THEN the system SHALL return 200 with `intervalMinutes: 60`.

### SC-004: Organizer changes interval to 30
GIVEN an event whose interval is not 30 WHEN the organizer sends
`PUT /events/{publicId}/params` with `intervalMinutes` `30` THEN the system
SHALL return 200 with `intervalMinutes: 30`.

### SC-005: Invalid interval value rejected
GIVEN an existing event WHEN the organizer sends
`PUT /events/{publicId}/params` with `intervalMinutes` `45` (or any value other
than 15/30/60) THEN the system SHALL return 400 with error code
`invalid_interval`.

### SC-006: Missing intervalMinutes rejected
GIVEN an existing event WHEN the organizer sends
`PUT /events/{publicId}/params` with body `{ "timezone": "Europe/Berlin" }`
(no `intervalMinutes`) THEN the system SHALL return 400 (request-shape).

### SC-007: Non-organizer forbidden
GIVEN an existing event WHEN a guest who is not the organizer sends
`PUT /events/{publicId}/params` with a valid body THEN the system SHALL return
403 `forbidden`.

### SC-008: Unknown event not found
GIVEN an unknown public id WHEN `PUT /events/{publicId}/params` is called with
a valid body THEN the system SHALL return 404 `event_not_found`.

### SC-009: Unknown timezone still rejected
GIVEN an existing event WHEN the organizer sends
`PUT /events/{publicId}/params` with a valid `intervalMinutes` and an unknown
`timezone` THEN the system SHALL return 400 `invalid_timezone`.

### SC-010: Aligned range stored as-is
GIVEN an event with interval 30 WHEN a participant submits a range whose start
and end already fall on 30-minute boundaries in their wall-clock timezone THEN
the system SHALL store the range unchanged and SHALL return the same wall-clock
values in the response.

### SC-011: Misaligned range snapped outward and echoed
GIVEN an event with interval 30 WHEN a participant submits the wall-clock range
`09:05–09:50` in their timezone THEN the system SHALL snap it to
`09:00–10:00`, store the corresponding UTC instants, and SHALL return the
snapped wall-clock values `09:00–10:00` in the response.

### SC-012: Snap occurs in wall-clock frame before UTC conversion
GIVEN an event with interval 30 and a submitter in `Asia/Kolkata` (`+05:30`)
WHEN the participant submits `09:05–09:50` wall-clock THEN the system SHALL
snap to `09:00–10:00` wall-clock (not to UTC-grid boundaries) and store the
UTC instants derived from the snapped wall-clock times.

### SC-013: DST gap in a snapped bound is rejected
GIVEN a submitter timezone with a DST spring-forward gap WHEN snapping a bound
places it inside the gap THEN the system SHALL reject with 400
`invalid_availability_range` (existing behavior, applied to the snapped value).

### SC-014: Snapped range over 24 hours rejected
GIVEN an event with interval 60 WHEN a participant submits a range that is
exactly 24 hours but whose outward rounding extends past 24 hours THEN the
system SHALL reject with 400 `invalid_availability_range` and SHALL NOT clamp
the range.

### SC-015: Bound exactly on a boundary is unchanged
GIVEN an event with interval 30 WHEN a participant submits a bound exactly on a
30-minute boundary THEN the system SHALL leave that bound unchanged (no
shifting of an already-aligned value).

### SC-016: Non-hour-offset submitter allowed off-grid UTC instant
GIVEN an event with interval 60 and a submitter in `Asia/Kolkata` (`+05:30`)
WHEN the participant submits a range aligned on their wall-clock hour grid THEN
the system SHALL accept it even though the resulting UTC instant is not on an
hour boundary.

### SC-017: Existing ranges returned verbatim after an interval change
GIVEN an event with stored availability ranges WHEN the organizer changes the
interval THEN availability reads SHALL return the stored ranges unchanged,
including any that no longer align to the new interval.

### SC-018: Interval not present on availability reads
GIVEN an event with a known interval WHEN any availability read endpoint is
called THEN the response SHALL NOT include `intervalMinutes` and its shape
SHALL match the pre-change contract.

## Frontend Handoff Document

The `frontend-handoff.md` deliverable SHALL state, as contract facts only (no
frontend planning or component design):

- the `PUT /events/{publicId}/params` request and response shape after this
  change, and that `intervalMinutes` is REQUIRED and must be exactly 15, 30,
  or 60;
- the `GET /events/{publicId}` response shape after this change, including
  `intervalMinutes`;
- that `POST /events` is unchanged in shape and that newly created events get
  `intervalMinutes: 30`;
- that submitted availability ranges are snapped (`start` down, `end` up) in
  the submitter's wall-clock timezone before conversion, and that the response
  echoes the snapped values;
- that a snapped range violating `end > start` or the 24-hour maximum is
  rejected with 400 `invalid_availability_range`, never clamped;
- that changing the interval does not retroactively re-align stored ranges;
- the complete list of error codes for the touched endpoints (including
  `invalid_interval` for the params endpoint);
- a note that the two `QUERY` availability read endpoints are absent from the
  generated TypeScript client.

## Acceptance Criteria

- [ ] An event's interval is always exactly 15, 30, or 60 minutes.
- [ ] A newly created event reports `intervalMinutes: 30` on `GET /events/{publicId}`.
- [ ] An existing event created before this change reports `intervalMinutes: 30` and its stored ranges are untouched.
- [ ] `POST /events` request shape is unchanged.
- [ ] `GET /events/{publicId}` response includes `intervalMinutes`.
- [ ] `PUT /events/{publicId}/params` requires both `timezone` and `intervalMinutes`.
- [ ] A params update with each allowed value (15/30/60) succeeds and echoes both `timezone` and `intervalMinutes`.
- [ ] A params update with an out-of-set interval returns 400 `invalid_interval`.
- [ ] A params update with missing `intervalMinutes` returns 400 (request-shape).
- [ ] A params update by a non-organizer returns 403 `forbidden`.
- [ ] A params update for an unknown event returns 404 `event_not_found`.
- [ ] A params update with an unknown timezone returns 400 `invalid_timezone`.
- [ ] Availability ranges are snapped `start` down / `end` up on the event's interval grid in the submitter's wall-clock timezone, before UTC conversion.
- [ ] Already-aligned bounds are stored unchanged.
- [ ] Snapped values are both stored as UTC instants and echoed in the response.
- [ ] A snapped range violating `end > start` or the 24-hour maximum is rejected with 400 `invalid_availability_range`, never clamped.
- [ ] Snapping cannot produce a zero-duration range.
- [ ] DST gap/ambiguous handling is applied to the snapped values.
- [ ] A 60-minute event accepts a `+05:30` submitter whose UTC instants fall off the hour grid.
- [ ] A range exactly 24 hours that snaps past 24 hours is rejected with 400 `invalid_availability_range`.
- [ ] Availability read shapes are unchanged and do not include `intervalMinutes`.
- [ ] Changing the interval leaves stored ranges verbatim (no retroactive re-alignment).
- [ ] `Docs/changes/004-events-time-intervals/frontend-handoff.md` exists and covers every item in the "Frontend Handoff Document" section.

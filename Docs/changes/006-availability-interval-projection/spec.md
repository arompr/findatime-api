# 006-availability-interval-projection: Spec

The **what**. Testable requirements and scenarios only — no implementation, no
architecture, no code.

## Requirements

### REQ-001: Reads reflect the event's current interval
WHEN availability is read THEN THE SYSTEM SHALL express the stored ranges on the
grid of the event's current interval (15, 30 or 60 minutes).

### REQ-002: Projection is inward
WHEN a stored range is projected THEN THE SYSTEM SHALL move its start up to the
next grid boundary and its end down to the previous grid boundary, in the
anchor frame defined by REQ-006.

### REQ-003: Ranges shorter than one interval disappear
WHEN a stored range contains no full interval between its snapped bounds THEN THE
SYSTEM SHALL omit it from the read result rather than widening, shrinking to a
partial range, or rejecting the read.

### REQ-004: Aligned bounds are unchanged
WHEN a stored bound already lies exactly on the current interval grid THEN THE
SYSTEM SHALL leave it unchanged.

### REQ-005: The union is projected, not each row
WHEN a participant has multiple stored ranges THEN THE SYSTEM SHALL merge
overlapping or touching ranges first and project the resulting union, so that
adjacent ranges which jointly cover a full interval produce that interval.

### REQ-006: The grid is anchored in the event's frame
WHEN projecting THEN THE SYSTEM SHALL anchor the grid at midnight in the event's
timezone; WHEN the event's timezone option is off or unset THEN THE SYSTEM SHALL
anchor at midnight UTC.

### REQ-007: Viewer timezone does not change the slots
WHEN two readers request the same availability in different viewer timezones
THEN THE SYSTEM SHALL produce the same absolute (UTC) projected ranges for both;
only the rendered wall-clock representation SHALL differ.

### REQ-008: Storage is never rewritten
WHEN the event's interval changes THEN THE SYSTEM SHALL leave every stored
availability row unchanged, and changing the interval back SHALL restore the
previously projected output.

### REQ-009: The write path is unchanged
WHEN a participant submits availability THEN THE SYSTEM SHALL keep snapping each
bound outward onto the event's interval at submission time and SHALL keep echoing
the stored snapped values.

### REQ-010: Read response shape is unchanged
WHEN any availability read endpoint responds THEN THE SYSTEM SHALL keep the
existing response shape and SHALL NOT add an `intervalMinutes` field or a new
endpoint.

### REQ-011: Both read endpoints agree
WHEN the same participant's availability is read through the event-wide endpoint
and the single-participant endpoint THEN THE SYSTEM SHALL apply the same
projection and return the same ranges for that participant (in each endpoint's
own viewer frame).

### REQ-012: Participants with nothing left still appear
WHEN every range of a participant is projected away THEN THE SYSTEM SHALL still
include that participant in the event-wide read with an empty range list, exactly
as a participant with no availability does today.

### REQ-013: Reads never fail because of DST
WHEN a projected grid boundary falls in a daylight-saving gap or repeated period
of the event's timezone THEN THE SYSTEM SHALL still return a result: a boundary in
a gap advances to the first valid instant (the end of the gap), and a repeated
boundary resolves to the interpretation that keeps the projection inward.

### REQ-014: Interval validation is unchanged
WHEN the interval is not one of 15, 30 or 60 THEN THE SYSTEM SHALL keep rejecting
it at the existing params/domain boundary; reads SHALL NOT introduce a new
validation path.

### REQ-015: Unchanged behaviour for unrelated reads
WHEN an event has no availability, an unknown event is read, or an unknown
participant is requested THEN THE SYSTEM SHALL keep the existing behaviour and
error mapping.

## Scenarios

### SC-001: Half-hour range under an hour grid
GIVEN a stored range `08:30–09:30` and an event interval of 60 THEN the read
SHALL omit the range (it contains no full hour).

### SC-002: Half-hour start under an hour grid
GIVEN a stored range `08:30–10:00` and an event interval of 60 THEN the read
SHALL return `09:00–10:00`.

### SC-003: Misaligned both ends
GIVEN a stored range `08:20–10:40` and an event interval of 60 THEN the read
SHALL return `09:00–10:00`.

### SC-004: Already aligned
GIVEN a stored range `09:00–10:00` and an event interval of 60 THEN the read
SHALL return `09:00–10:00` unchanged.

### SC-005: Interval change is reflected on read
GIVEN ranges submitted while the interval was 30 WHEN the organizer changes the
interval to 60 THEN both read endpoints SHALL apply the 60-minute projection
without any stored row being modified.

### SC-006: Interval change is reversible
GIVEN the state of SC-005 WHEN the organizer changes the interval back to 30
THEN the reads SHALL again return the ranges as stored.

### SC-007: Adjacent ranges form a slot
GIVEN stored ranges `08:30–09:30` and `09:30–10:30` and an event interval of 60
THEN the read SHALL return `09:00–10:00`.

### SC-008: Overlapping ranges merge
GIVEN stored ranges `08:00–09:30` and `09:00–10:30` and an event interval of 60
THEN the read SHALL return a single range `08:00–10:00`.

### SC-009: Event frame anchors the grid
GIVEN an event in `Europe/Berlin` with a stored range that is aligned to Berlin
hours but off the UTC hour WHEN read in a different viewer timezone THEN the
projected absolute range SHALL be the Berlin-aligned one.

### SC-010: Timezone option off anchors on UTC
GIVEN an event with the timezone option off WHEN availability is read THEN the
grid SHALL be anchored at midnight UTC and the rendered frame SHALL remain UTC.

### SC-011: Everything projected away
GIVEN a participant whose only stored range is `08:30–09:30` and an event
interval of 60 WHEN the event-wide read is served THEN the participant SHALL be
present with an empty range list.

### SC-012: DST gap
GIVEN an event in a timezone with a spring-forward gap and a stored range whose
projected start would land in the gap WHEN the read is served THEN it SHALL
return `200` with the boundary advanced to the end of the gap, never an error.

### SC-013: Write path unchanged
GIVEN an event with interval 30 WHEN a participant submits `09:05–09:50` THEN the
response SHALL still echo `09:00–10:00` and that SHALL be what is stored.

### SC-014: Read shape unchanged
GIVEN any completed availability read WHEN the response is inspected THEN it
SHALL carry only the existing fields (`timezone`, `participants`/`ranges` or the
single-participant equivalents) and no interval field.

## Acceptance Criteria

- [ ] Availability reads project stored ranges inward onto the event's current
      interval grid.
- [ ] `08:30–09:30` under interval 60 yields no range; `08:30–10:00` yields
      `09:00–10:00`; `08:20–10:40` yields `09:00–10:00`.
- [ ] Aligned bounds are unchanged.
- [ ] The union of a participant's ranges is projected, so adjacent ranges that
      jointly cover a full interval yield it.
- [ ] The grid is anchored in the event's timezone (UTC when the option is off),
      and viewer timezones do not change the absolute projected ranges.
- [ ] Changing the interval modifies no stored row, and changing it back restores
      the previous projection.
- [ ] The write path and its echoed snapped values are unchanged.
- [ ] Availability read response shapes are unchanged.
- [ ] Both read endpoints apply the same projection.
- [ ] A participant whose ranges all project away still appears with an empty
      list in the event-wide read.
- [ ] DST gaps/ambiguity never fail a read.
- [ ] Existing availability read/write tests continue to pass.

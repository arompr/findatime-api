# 007-events-day-window: Spec

The **what**. Testable requirements and scenarios only — no implementation,
no architecture, no code.

## Requirements

### REQ-001: Day window is an event param
WHEN an event exists THEN THE SYSTEM SHALL hold a day window for it, made of a
start hour and an end hour in whole hours, where `0 <= dayStartHour <
dayEndHour <= 24` and `24` denotes the end of the day.

### REQ-002: Default window on creation
WHEN an event is created THEN THE SYSTEM SHALL set its day window to 09:00–21:00
(`dayStartHour = 9`, `dayEndHour = 21`), and the create request SHALL NOT
accept a day window.

### REQ-003: Default window for pre-existing events
WHEN the change is deployed THEN THE SYSTEM SHALL report every event created
before it with the window 09:00–21:00, without rewriting any availability.

### REQ-004: Organizer replaces the window with the other params
WHEN the organizer sends `PUT /events/{publicId}/params` with `timezone`,
`intervalMinutes`, `dayStartHour`, and `dayEndHour` THEN THE SYSTEM SHALL
replace all four params and respond 200 with the four stored values.

### REQ-005: Window fields are required
WHEN `dayStartHour` or `dayEndHour` is missing from the params request THEN THE
SYSTEM SHALL respond 400 with the plain message `"dayStartHour is required"` or
`"dayEndHour is required"` and change nothing.

### REQ-006: Invalid window is rejected
WHEN `dayStartHour` is outside 0–23, `dayEndHour` is outside 1–24, or
`dayStartHour >= dayEndHour` THEN THE SYSTEM SHALL respond 400 with error code
`invalid_day_window` and change nothing.

### REQ-007: Validation order
WHEN a params request carries more than one invalid value THEN THE SYSTEM SHALL
report the interval first, then the day window, then the timezone.

### REQ-008: Window is returned on event retrieval
WHEN `GET /events/{publicId}` is called THEN THE SYSTEM SHALL include
`dayStartHour` and `dayEndHour` in the response.

### REQ-009: Window is display-only
WHEN availability is submitted or read THEN THE SYSTEM SHALL behave exactly as
before this change, regardless of the event's day window: ranges outside the
window are accepted, stored, and returned unclipped.

### REQ-010: Changing the window never touches availability
WHEN the organizer changes the day window THEN THE SYSTEM SHALL NOT modify any
stored availability range.

### REQ-011: Window frame
WHEN the event's timezone option is on THEN the day window SHALL be interpreted
as wall-clock hours in the event's timezone; WHEN it is off THEN it SHALL be
interpreted in the same frame availability is rendered in for such events (the
`UTC` label).

### REQ-012: Organizer-only
WHEN a non-organizer changes the params THEN THE SYSTEM SHALL respond 403
`forbidden` and leave the window unchanged.

## Scenarios

### SC-001: New event gets the default window
GIVEN no event WHEN a guest creates one THEN `GET /events/{publicId}` returns
`dayStartHour: 9` and `dayEndHour: 21`.

### SC-002: Organizer narrows to office hours
GIVEN an event with the default window WHEN the organizer PUTs params with
`dayStartHour: 9, dayEndHour: 17` (and the current timezone and interval) THEN
the response is 200 with `dayStartHour: 9, dayEndHour: 17` and a later GET
returns the same.

### SC-003: Full-day window
GIVEN an event WHEN the organizer PUTs `dayStartHour: 0, dayEndHour: 24` THEN
the response is 200 and the window is stored as 0–24.

### SC-004: Inverted window
GIVEN an event WHEN the organizer PUTs `dayStartHour: 17, dayEndHour: 9` THEN
the response is 400 `invalid_day_window` and the stored window is unchanged.

### SC-005: Empty window
GIVEN an event WHEN the organizer PUTs `dayStartHour: 10, dayEndHour: 10` THEN
the response is 400 `invalid_day_window`.

### SC-006: Out-of-range hours
GIVEN an event WHEN the organizer PUTs `dayStartHour: -1` or `dayEndHour: 25`
or `dayStartHour: 24` THEN the response is 400 `invalid_day_window`.

### SC-007: Missing field
GIVEN an event WHEN the organizer PUTs params without `dayEndHour` THEN the
response is 400 with message `"dayEndHour is required"`.

### SC-008: Interval error wins over window error
GIVEN an event WHEN the organizer PUTs `intervalMinutes: 20, dayStartHour: 17,
dayEndHour: 9` THEN the response is 400 `invalid_interval`.

### SC-009: Window error wins over timezone error
GIVEN an event WHEN the organizer PUTs `timezone: "Mars/Olympus",
intervalMinutes: 30, dayStartHour: 17, dayEndHour: 9` THEN the response is 400
`invalid_day_window`.

### SC-010: Non-organizer
GIVEN an event and a joined participant WHEN the participant PUTs a valid window
THEN the response is 403 `forbidden` and the window is unchanged.

### SC-011: Availability outside the window survives
GIVEN a participant with a stored range 07:00–08:00 and a window 09:00–21:00
WHEN availability is read THEN the 07:00–08:00 range is returned; AND WHEN the
organizer later changes the window to 06:00–12:00 THEN the stored range is
unchanged.

## Acceptance Criteria

- [ ] A newly created event reports `dayStartHour = 9`, `dayEndHour = 21`.
- [ ] An event created before the migration reports 9/21 after it.
- [ ] `PUT /events/{publicId}/params` requires and echoes `dayStartHour` and
      `dayEndHour`.
- [ ] Any window outside `0 <= start < end <= 24` is rejected with 400
      `invalid_day_window`, and nothing is stored.
- [ ] A missing window field is a request-shape 400 with a plain message.
- [ ] Validation order is interval → day window → timezone.
- [ ] `GET /events/{publicId}` returns both fields.
- [ ] Availability writes and reads are unaffected by the window.
- [ ] Only the organizer can change the window.
- [ ] The regenerated TypeScript client carries both fields on
      `GetEventResponse`, `UpdateEventParamsRequestParams`, and
      `UpdateEventParamsResponse`.

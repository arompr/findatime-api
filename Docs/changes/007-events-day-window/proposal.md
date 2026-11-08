# 007-events-day-window: Proposal

## Goal

Let the organizer choose which hours of the day an event's calendar covers, for
example 09:00–17:00 for a work meeting or 10:00–19:00 for a weekend activity.
Today the backend has no such setting, so every client hard-codes a range. The
range is either too narrow for evening events or too wide and noisy for office
meetings.

The change adds a **day window** to the event params: a start hour and an end
hour, in whole hours, stored per event. The organizer edits it with the other
params, and it is returned wherever the params are returned. New events default
to **09:00–21:00**.

## Scope

In scope:

- `dayStartHour` (0–23) and `dayEndHour` (1–24) on `EventParams`, with
  `dayStartHour < dayEndHour`. `24` means end of day (midnight).
- Defaults 9 and 21 on creation. Existing events get the same values through the
  migration's column defaults.
- `PUT /events/{publicId}/params` requires both fields (full replace) and echoes
  them back.
- `GET /events/{publicId}` returns both fields.
- A new error `400 invalid_day_window` for out-of-range or inverted hours.
  Missing fields are inline request-shape 400s.
- Migration adding `events.day_start_hour` / `events.day_end_hour` (non-null,
  defaults 9/21) and a DB check constraint.
- Unit, integration, and Hurl coverage. Regenerate the TypeScript client.
- Fold the deltas into `Docs/specs/events/` on archive.

Out of scope:

- Any enforcement on availability. Writes still accept ranges outside the window,
  and reads still return them unclipped. Changing the window never touches stored
  availability, the same rule as for the interval.
- Sub-hour bounds (e.g. 08:30) and windows that cross midnight (e.g. 22:00–02:00).
- Per-weekday windows or per-participant windows.
- Accepting the window on `POST /events` (create stays defaults-only, like the
  interval).
- Adding params to `GET /events?guestId=`.

## Rationale

- **Display-only keeps the change small and safe.** The window says what the
  organizer wants to see. It is not a rule about participants. Clipping or
  rejecting availability would destroy entered data when the organizer narrows the
  window, and it would raise frame questions for legacy events that have no
  timezone.
- **Whole hours never conflict with the interval.** Every hour boundary sits on a
  15, 30, or 60-minute grid, so changing the interval can never make a stored
  window invalid. Both examples given (9–17, 10–19) are whole hours.
- **09:00–21:00 fits most events without an edit.** It covers the working day and
  after-work or evening plans (dinners, drinks, sports). At the default 30-minute
  interval that is 24 rows, which is still manageable. 9–17 would force an edit for
  every social event. Starts before 09:00 are rarer than evening events.
- **Required fields keep the endpoint a true full replace,** consistent with how
  `intervalMinutes` was added in 004.

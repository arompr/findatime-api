# 008-availability-utc-instants: Proposal

## Context

### The problem

A Vancouver viewer on event `IKQN3ySKQJFl` (event timezone `America/New_York`,
hours 8–21, 30-min slots) sees availability blocks one hour off relative to the
event window for every date from Nov 1, 2026 onward. Dates before Nov 1 are
correct.

Root cause: two different timezone databases convert the same data in opposite
directions.

- The system tz database (tzdata 2026b, used by the API through
  .NET/`TimeZoneInfo`) has British Columbia on permanent UTC-7 from Nov 1, 2026
  (`zdump` labels it "MST").
- The browser's ICU (headless Chromium 148; Node's ICU is tz 2025b) still has
  PST, UTC-8.
- Data flow before this change:
  1. The UI requests availability in the browser frame (`queries.ts`,
     `browserTimezone()`).
  2. The API stores UTC instants (`availabilities.start_utc/end_utc`,
     `timestamptz`) and converts them to Vancouver wall-clock with server rules:
     14:00Z becomes 07:00.
  3. The UI converts the wall-clock back to an instant with browser rules
     (`wallClockToInstant`): 07:00 becomes 15:00Z, an hour late.
  4. The event window (`lib/dayWindow.ts` `localDayWindow`) is computed with
     browser rules only: 8 AM New York shows as 5 AM.
- Result: blocks drift an hour from the window and from what other participants
  see. A New York participant's 9 AM shows on the Vancouver grid as 10 AM New
  York time. A Vancouver user who marks "7 AM" is saved as 9 AM for New York,
  while their grid suggested 10 AM.

Evidence: `QUERY /events/IKQN3ySKQJFl/availabilities` returned Test 1's Nov 3
range as 07:00 in Vancouver, 09:00 in New York and 14:00 in UTC. Chromium
renders 14:00Z as 6:00 AM PST.

### Related defects in the same path

- **Snapping happens in the wrong frame.** `SetAvailability` snapped in the
  request (viewer) frame (`Interval.SnapStart/SnapEnd` on the wall-clock), but
  `AvailabilityProjection` snaps reads in the event frame. For viewers whose UTC
  offset includes 30 or 45 minutes (Kolkata, St. John's, Kathmandu) on 60-min
  events, saved ranges could be trimmed or removed on read. Found by reading the
  code; not reproduced.
- **Participant timezone is overwritten on every save.** `SetAvailability`
  called `participant.SetTimezone(request.timezone)` each time; the
  participant's timezone should be the one recorded at join.
- **Confusing header chip** (UI). It showed the hours in the viewer's local time
  ("5 AM – 6 PM") next to an "America/New York" chip, so it read as New York
  hours.

### Intended outcome

Wall-clock math happens once on each side: the server snaps in the event
timezone, and the browser converts only for display. Nothing is converted on
one side and converted back on the other. A browser with outdated tz rules can
still label times differently from the server, but blocks, the event window and
the "now" line stay aligned with each other and with other participants.

## Goal

The availability API exchanges UTC instants (breaking change):

- Ranges are ISO 8601 instants with an explicit offset:
  `{ "start": "2026-11-03T14:00:00Z", "end": "2026-11-03T18:00:00Z" }`. The
  server answers in UTC (`+00:00`).
- Every `timezone` field goes from the availability requests and responses.
- The two `QUERY` endpoints become plain `GET`, since they no longer have a body.
- `PUT …/availability` takes `{ ranges }` and returns the stored, snapped
  instants.
- The server snaps writes outward in the event's timezone (UTC for legacy
  events), the same frame reads project in.
- Saving availability no longer touches the participant's timezone.

## Scope

In scope: the API contract and use cases, `AvailabilityProjection.SnapOutward`,
removal of the now-unused `Interval.SnapStart/SnapEnd` and
`TimezoneConverter.ToUtc/ToWallClock`, tests, the Hurl workflow, the regenerated
TypeScript client (0.1.0, `src/query.ts` deleted), feature specs and agent docs.
The UI side (dependency bump, API boundary, header chip) is in
`frontend-handoff.md`.

Out of scope:

- No data migration. Storage is already UTC instants; rows saved before this
  change were snapped in the viewer frame, and `Project` re-snaps them in the
  event frame on read.
- `localDayWindow` is computed for today only and ilamy accepts a single window,
  so in a week that crosses a DST change days on the other side can show the
  window an hour off. Tracked separately.
- Browsers with outdated tz rules still label times by their own clock; that is
  unavoidable without bundling tz data.

## Rationale

- **Instants are frame-free.** Once nothing is converted and converted back,
  disagreement between tz databases can only change labels, never positions.
- **One frame for write and read.** Snapping writes where reads project means a
  saved range always survives the next read.
- **Outward on write, inward on read** keeps the existing guarantees: a write
  never loses selected time, and a read never shows a partial slot.
- **A single consumer** (findatime-ui) makes a breaking change with a short
  deploy window cheaper than versioning the endpoints.

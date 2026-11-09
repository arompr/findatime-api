# Frontend Handoff — Availability as UTC instants (008)

Contract facts only. Breaking change: requires
`@arompr/findatime-typescript-client@^0.1.0`.

## 1. Ranges are instants

Every availability range, in requests and responses, is

```json
{ "start": "2026-11-03T14:00:00Z", "end": "2026-11-03T18:00:00Z" }
```

- Requests: ISO 8601 with an explicit offset (`Z` or `±hh:mm`). A value without
  an offset (`"2026-11-03T14:00:00"`) is rejected with 400
  `invalid_availability_range`.
- Responses: always UTC, serialized as `+00:00`
  (`"2026-11-03T14:00:00+00:00"`). Normalize with `new Date(x).toISOString()`
  (or `dayjs(x).toISOString()`) before using values as keys.
- There is **no `timezone` field** anywhere in the availability API.

## 2. Reads are GET

| Before | Now |
| ------ | --- |
| `QUERY /events/{publicId}/availabilities` body `{ timezone }` | `GET /events/{publicId}/availabilities` |
| `QUERY /events/{publicId}/participants/{id}/availability` body `{ timezone }` | `GET /events/{publicId}/participants/{id}/availability` |

`GET …/availabilities` → `{ participants: [{ participantId, name, ranges }] }`
(no top-level `timezone`). `GET …/participants/{id}/availability` →
`{ participantId, name, ranges }`. The generated SDK provides both; the
hand-written `query.ts` is gone.

## 3. Write

`PUT /events/{publicId}/participants/{participantId}/availability`,
header `X-Guest-Id`, body `{ "ranges": [{ "start", "end" }] }` (no `timezone`).
Response `{ participantId, name, ranges }`: the stored ranges, snapped outward
to the event's slot grid **in the event's timezone** (UTC if the event has
none). A misaligned range comes back wider; e.g. a Kolkata viewer's
13:30–14:30Z on a 60-min New York event comes back as 13:00–15:00Z.

## 4. Participant timezone

Saving availability no longer changes the participant's timezone. It is set
only by `POST /events/{publicId}/join` (`timezone`, unchanged).

## 5. What the client should do

- Send the draft's instants as-is; don't convert to wall clock.
- Convert instants to local time only to display them.
- The event's `timezone` + `dayStartHour/dayEndHour` + `intervalMinutes` still
  define the grid; the server is the authority on alignment.

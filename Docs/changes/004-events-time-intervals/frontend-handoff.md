# Frontend Handoff — Events Time Intervals (004)

Contract facts only. No frontend planning or component design.

## 1. `PUT /events/{publicId}/params` — full replace of both params

Request (both fields are sent on every call):

```json
{
  "timezone": "Europe/Berlin",
  "intervalMinutes": 15
}
```

- `intervalMinutes` is **REQUIRED** and must be exactly one of `15`, `30`, or `60`.
- `timezone` is a nullable IANA timezone string (`null` disables the event timezone); the value is otherwise unchanged from before this change.
- The request is authenticated with the organizer's `X-Guest-Id` header. Only the organizer may call it.

Response (200):

```json
{
  "timezone": "Europe/Berlin",
  "intervalMinutes": 15
}
```

The response echoes the stored `timezone` and `intervalMinutes`.

Error codes for this endpoint:

| Status | `error` code                  | Condition |
| ------ | ----------------------------- | --------- |
| 400    | `invalid_interval`            | `intervalMinutes` present but not 15/30/60 |
| 400    | `invalid_timezone`            | `timezone` is a non-null unknown IANA id |
| 400    | *(none — plain message)*      | request shape: `intervalMinutes` missing → `"intervalMinutes is required"` |
| 400    | *(none — plain message)*      | request shape: `X-Guest-Id` header missing/invalid |
| 403    | `forbidden`                   | caller is not the organizer |
| 404    | `event_not_found`             | event does not exist |

## 2. `GET /events/{publicId}` — response now includes `intervalMinutes`

```json
{
  "publicId": "<id>",
  "name": "Saturday Night Local",
  "isPasscodeProtected": false,
  "timezone": "Europe/Berlin",
  "intervalMinutes": 30
}
```

- `intervalMinutes` is always present and is one of `15`/`30`/`60`.
- Existing fields (`publicId`, `name`, `isPasscodeProtected`, `timezone`) are unchanged.
- Error code: 404 `event_not_found`.

## 3. `POST /events` — unchanged; new events default to 30

- Request shape is unchanged (no `intervalMinutes` field is sent or accepted).
- Every newly created event reports `"intervalMinutes": 30`.

## 4. Availability writes are snapped before conversion, and echoed

When a participant submits availability ranges (`PUT /events/{publicId}/participants/{participantId}/availability`), each range's `start` is snapped **down** and `end` is snapped **up** onto the event's interval grid, computed in the **submitter's wall-clock timezone** (the `timezone` field of the request body), *before* the wall-clock → UTC conversion is applied.

- Snapped values are what gets stored.
- The response echoes the **snapped** wall-clock values, so render what the response returns — not what was sent.

Example: a 30-minute event, submitter timezone `Europe/Berlin`, range `18:05–18:50`:

```json
{
  "timezone": "Europe/Berlin",
  "ranges": [
    { "start": "2026-09-21T18:05:00", "end": "2026-09-21T18:50:00" }
  ]
}
```

responds with (note the snapped bounds):

```json
{
  "participantId": "<id>",
  "name": "Bob",
  "timezone": "Europe/Berlin",
  "ranges": [
    { "start": "2026-09-21T18:00:00", "end": "2026-09-21T19:00:00" }
  ]
}
```

## 5. Snapped ranges that break invariants are rejected, never clamped

If the snapped range violates `end > start` or exceeds the 24-hour maximum, the request is rejected with **400 `invalid_availability_range`** and nothing is stored. The system never silently clamps a bound back to the grid or to a limit.

Note: outward rounding can push a bound past the 24-hour maximum (e.g. a range exactly 24 hours whose `end` ceils upward). Such a range is rejected, not accepted-with-clamp.

## 6. Changing the interval does not retroactively re-align stored ranges

When the organizer changes the event's interval, all stored availability ranges are left unchanged. Availability reads return the stored ranges verbatim, even if they no longer align to the new interval grid. Render them as-is.

## 7. Error codes for the touched endpoints

`PUT /events/{publicId}/params`:

- 400 `invalid_timezone`
- 400 `invalid_interval`
- 400 *(request-shape, plain message, e.g. `"intervalMinutes is required"`)*
- 403 `forbidden`
- 404 `event_not_found`

`GET /events/{publicId}`:

- 404 `event_not_found`

`PUT /events/{publicId}/participants/{participantId}/availability`:

- 400 *(request-shape, plain message: `"timezone is required"`, `"X-Guest-Id header is required"`, `"publicId is required"`, `"participantId must be a valid uuid"`)*
- 400 `invalid_timezone`
- 400 `invalid_availability_range`
- 403 `forbidden`
- 404 `event_not_found`
- 404 `participant_not_found`

## 8. Generated TypeScript client

- The regenerated client **does** carry `intervalMinutes` on the event and params models (`GetEventResponse`, `UpdateEventParamsRequestParams`, `UpdateEventParamsResponse`).
- The two `QUERY` availability read endpoints — `QUERY /events/{publicId}/availabilities` and `QUERY /events/{publicId}/participants/{participantId}/availability` — remain **absent** from the generated TypeScript client (a pre-existing OpenAPI limitation: the non-standard `QUERY` verb is not represented). The frontend must call those two endpoints manually.

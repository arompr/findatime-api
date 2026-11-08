# Frontend Handoff — Event Day Window (007)

This lists contract facts only. It does not plan frontend work or design
components.

An event now has a **day window**: the hours of the day its calendar covers, for
example 09:00–17:00. It is a whole-hour setting stored on the event, changed by
the organizer, and **display-only**: the backend does not use it to filter or
validate availability.

## 1. Fields

| Field          | Type    | Range  | Meaning |
| -------------- | ------- | ------ | ------- |
| `dayStartHour` | integer | 0–23   | first hour shown (inclusive), e.g. `9` = 09:00 |
| `dayEndHour`   | integer | 1–24   | hour the calendar stops at (exclusive), e.g. `17` = calendar ends at 17:00; `24` = midnight / end of day |

Always `dayStartHour < dayEndHour`. Hours are whole numbers, so 08:30 is not
possible. A window cannot cross midnight. Every hour boundary lines up with every
interval (15/30/60), so there are always `(dayEndHour - dayStartHour) * 60 /
intervalMinutes` rows.

**Default for every event: `dayStartHour = 9`, `dayEndHour = 21`** (09:00–21:00).
This applies to new events and to events that existed before this change.

## 2. `GET /events/{publicId}` — two new fields

```json
{
  "publicId": "<id>",
  "name": "Saturday Night Local",
  "isPasscodeProtected": false,
  "timezone": "Europe/Berlin",
  "intervalMinutes": 30,
  "dayStartHour": 9,
  "dayEndHour": 21
}
```

Both fields are always present. Nothing else changed. `GET /events?guestId=`
still carries no params.

## 3. `PUT /events/{publicId}/params` — BREAKING: two new required fields

The endpoint is still a **full replace**, so send all four fields every time
(take the current values from `GET` for the fields the user did not touch).
It is organizer-only, with the `X-Guest-Id` header.

Request:

```json
{
  "timezone": "Europe/Berlin",
  "intervalMinutes": 30,
  "dayStartHour": 9,
  "dayEndHour": 17
}
```

Response (200) echoes the stored values:

```json
{
  "timezone": "Europe/Berlin",
  "intervalMinutes": 30,
  "dayStartHour": 9,
  "dayEndHour": 17
}
```

Errors:

| Status | `error`              | When |
| ------ | -------------------- | ---- |
| 400    | `invalid_interval`   | `intervalMinutes` not 15/30/60 (checked first) |
| 400    | `invalid_day_window` | `dayStartHour` not in 0–23, `dayEndHour` not in 1–24, or `dayStartHour >= dayEndHour` (checked second) |
| 400    | `invalid_timezone`   | unknown IANA id (checked third) |
| 400    | *(plain message)*    | `"dayStartHour is required"`, `"dayEndHour is required"`, `"intervalMinutes is required"`, missing or invalid `X-Guest-Id` |
| 403    | `forbidden`          | caller is not the organizer |
| 404    | `event_not_found`    | event does not exist |

Error bodies keep the usual shape `{ "message": "...", "error": "invalid_day_window" }`.
A rejected request changes nothing. A frontend that still sends only `timezone`
and `intervalMinutes` gets `400 "dayStartHour is required"`, so ship this update
together with the API deploy.

## 4. `POST /events` — unchanged

The create request does not accept a window. Every new event starts at 9/21. To
use a different window, call `PUT /params` after creation.

## 5. Which timezone the hours are in

The backend stores two plain hours and never converts them. They are wall-clock
hours in the **event's anchor frame**:

- Event `timezone` set → hours are in that timezone. `9`–`17` with
  `Europe/Berlin` means 09:00–17:00 Berlin time. If you render the calendar in a
  viewer's own timezone, converting the window to that frame is up to the client.
  It can shift, and it can cross midnight for distant viewers.
- Event `timezone` is `null` (legacy) → hours are in the same frame the
  availability reads use for such events (label `UTC`).

## 6. Availability is not affected

- `PUT .../participants/{participantId}/availability` still accepts ranges
  outside the window. There is no new error.
- Both `QUERY` availability reads still return every projected range, including
  parts outside the window. The API does not clip them. Deciding what to show
  for ranges outside the window is a frontend choice.
- Changing the window never modifies stored availability. Widening it again shows
  the earlier data.

## 7. Generated TypeScript client

After `make typescript-client`, `dayStartHour` and `dayEndHour` appear on
`GetEventResponse`, `UpdateEventParamsRequestParams`, and
`UpdateEventParamsResponse`. They are typed `number | string`, like the existing
`intervalMinutes`, because .NET's OpenAPI output allows numeric strings for
integers. The API always returns JSON numbers. On the request type they are
`null | number | string` because the backend uses `int?` to detect missing
fields. They are still required at runtime: send integers, never `null`. The two
`QUERY` availability endpoints remain absent from the client, as before.

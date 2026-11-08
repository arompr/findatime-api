# 007-events-day-window: Design

## Summary

Add a whole-hour day window to the `EventParams` value object, mirroring how
`IntervalMinutes` was added in 004. A static `DayWindow` helper in the Events
domain owns the bounds, the defaults (9/21), and validation. `EventParams`
validates through it, so an invalid window cannot exist in memory. Two non-null
integer columns with database defaults and a check constraint persist it. The
params endpoint and the event read surface the two fields. Availability code is
not touched.

## Changes

### Domain

- **New** `Features/Events/Domain/DayWindow.cs`: a static class like
  `Common/Interval`:
  - `public const int DefaultStartHour = 9;`
  - `public const int DefaultEndHour = 21;`
  - `IsValid(int startHour, int endHour)` →
    `startHour >= 0 && endHour <= 24 && startHour < endHour`
  - `Validate(int startHour, int endHour)` throws `InvalidDayWindowException`.
  It lives in the Events feature, not `Common/`, because availability does not
  use it (display-only).
- **New** `Features/Events/Domain/InvalidDayWindowException.cs`: message
  `"Day window must satisfy 0 <= dayStartHour < dayEndHour <= 24."` It sits next
  to its helper, the same way `InvalidIntervalException` sits next to `Interval`.
- `EventParams` gains `int DayStartHour` and `int DayEndHour`. The constructor
  becomes `EventParams(string? timezone, int intervalMinutes, int dayStartHour,
  int dayEndHour)`, which calls `Interval.Validate` and then `DayWindow.Validate`.
  Calling them in that order is what gives the interval → window → timezone
  validation order (timezone is resolved after construction in the use case).
  The fields stay flat, without a nested record, so the EF complex-property
  mapping remains one level deep.
- `Event.Create` passes `DayWindow.DefaultStartHour` and
  `DayWindow.DefaultEndHour`.

Invariant: whole hours always sit on every allowed interval grid (15/30/60), so
the window and the interval are independent. No cross-validation is needed, and
changing one can never invalidate the other.

### Persistence

- `DbContext` maps, inside the existing `ComplexProperty(e => e.Params, ...)`:
  - `DayStartHour` → `day_start_hour`, `.HasDefaultValue(DayWindow.DefaultStartHour)`, required
  - `DayEndHour` → `day_end_hour`, `.HasDefaultValue(DayWindow.DefaultEndHour)`, required
- Add a table check constraint `ck_events_day_window`:
  `"day_start_hour" >= 0 AND "day_end_hour" <= 24 AND "day_start_hour" < "day_end_hour"`
  (as defense in depth, like the availabilities check constraints).
- **Migration** `AddEventDayWindow`: two `integer NOT NULL` columns with defaults
  9 and 21, plus the check constraint. Existing rows pick up the defaults. There
  is no backfill, and availability is not rewritten.

### Application

- `UpdateEventParams.Execute(publicId, guestId, timezone, intervalMinutes,
  dayStartHour, dayEndHour)`:
  load → organizer check (403) → `new EventParams(...)` (interval, then window)
  → `TimezoneConverter.Resolve` when non-null → `SetParams` → `Save`. It returns
  all four values.
- `CreateEvent` is unchanged (the defaults come from `Event.Create`).

### API

- `UpdateEventParamsRequestParams(string? Timezone, int? IntervalMinutes, int?
  DayStartHour, int? DayEndHour)`. The nullable ints let the endpoint tell
  "missing" apart from a value.
- The endpoint adds inline checks after the `intervalMinutes` one:
  `"dayStartHour is required"`, `"dayEndHour is required"`.
- `UpdateEventParamsResponse(string? Timezone, int IntervalMinutes, int
  DayStartHour, int DayEndHour)`.
- `GetEventResponse(..., string? Timezone, int IntervalMinutes, int
  DayStartHour, int DayEndHour)`.
- `ExceptionHandler`:
  `[typeof(InvalidDayWindowException)] = (400, "invalid_day_window")`.

### Read side

- `get_event_by_public_id.sql` selects `day_start_hour, day_end_hour`.
- `EventDto` gains `int DayStartHour, int DayEndHour`, and `ReadEventService`
  reads both ordinals.
- `search_events.sql` and the list response are unchanged.

### Frame semantics (documentation only)

The backend never converts the window. It stores two wall-clock hours, which mean
"hours in the event's anchor frame": the event timezone when the option is on,
and the `UTC`-labelled frame that legacy reads already use when it is off. That
is the same anchor the 006 projection grid uses. Rendering the window in a
viewer's own frame is a client concern.

### DI

No new services. `DayWindow` is static, so neither
`Composition/ServiceCollectionExtensions.cs` nor
`tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` changes.

### Tests

| Area | Test | Kind |
| --- | --- | --- |
| `DayWindow` | valid: 9–21, 0–24, 0–1, 23–24; invalid: start = end, start > end, −1, 24–24, end 25, end 0 | unit (`Common`-style, new `Domain/DayWindowTests.cs`) |
| `EventParams` | constructor throws `InvalidDayWindowException`; interval checked before window | unit (`EventTests`) |
| `Event.Create` | defaults 9/21 | unit (`EventTests`) |
| Persistence | window round-trips; pre-existing default via `HasDefaultValue` | integration (`EventRepositoryTests`) |
| Create | new event reads back 9/21 | integration (`CreateEventTests` / `GetEventTests`) |
| Update params | replace, full day, invalid (inverted, empty, out of range), interval-before-window, window-before-timezone, 403, 404, stored value unchanged on error | integration (`UpdateEventParamsTests`) |
| Get event | returns window | integration (`GetEventTests`) |
| Availability untouched | existing availability tests pass unchanged | integration (no new test) |
| Wire | defaults on GET, PUT echoes window, `invalid_day_window` | Hurl (`events.hurl`) |

## Migration / Rollout

1. Generate `AddEventDayWindow` with `dotnet ef migrations add AddEventDayWindow
   --project src/FindAtime.Api/FindAtime.Api.csproj`. Check that the generated
   `Up` has the two defaulted columns and the check constraint.
2. Applying it (`dotnet ef database update`, local or staging) is a destructive
   step. Get the user's confirmation first.
3. `make typescript-client` to regenerate the hey-api client.
4. Breaking contract: `PUT /events/{publicId}/params` now requires two more
   fields. Deploy the API and the frontend update together. An old frontend gets
   400 `"dayStartHour is required"` on params save. Reads stay backward compatible
   because they only add fields.

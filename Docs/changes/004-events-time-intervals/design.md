# 004-events-time-intervals: Design

## Summary

Add **interval** — the calendar step granularity of an event, constrained to the
closed set `15 / 30 / 60` minutes — as a second event-level param alongside
`timezone`:

- `EventParams` grows an `IntervalMinutes` value; `Event.Create` defaults it to
  `30`; `Event.SetParams` keeps replacing the whole value object.
- A new non-null `events.interval_minutes` integer column (default `30`) is
  added via an EF Core migration; existing rows land on `30` via the column
  default, so **no backfill script and no re-writing of stored availability**.
- `PUT /events/{publicId}/params` becomes a full-replace over **both** fields
  (`timezone`, required `intervalMinutes`); `GET /events/{publicId}` returns
  `intervalMinutes`. Out-of-set interval values are rejected with a new
  `InvalidIntervalException` → `400 invalid_interval`.
- `SetAvailability` **snaps** each submitted range's `start` down and `end` up
  onto the event's interval grid, computed in the submitter's wall-clock
  timezone **before** the existing wall-clock → UTC conversion. Snapped values
  are stored and echoed; a snapped range violating `end > start` or `≤ 24h` is
  rejected with the existing `400 invalid_availability_range`, never clamped.

No change to `POST /events`, the availability read endpoints, or the event
search/list endpoint. Availability reads keep returning stored ranges verbatim
(no retroactive re-alignment when the interval changes).

## Resolved decisions

### D1 — One owner for the allowed set, the default, and the grid: a static `Common/Interval`

A single pure, stateless `static class Interval` owns everything about the
interval, so the closed set, the default, and the snap arithmetic can never
drift apart. It lives in `Common/` (alongside `TimezoneConverter`) because it is
used by both the Events domain (`EventParams` validation) and the Availability
application (`SetAvailability` snapping). It is **static** — no DI registration
in `ServiceCollectionExtensions.cs` or `IntegrationTest.cs` is needed (this
deviates from research.md's guess of a registered "validator service"; a static
helper is simpler and unit-testable directly).

Exact surface:

```
static class Interval
{
    const int DefaultMinutes = 30;
    static readonly IReadOnlySet<int> Allowed = new HashSet<int> { 15, 30, 60 };
    static bool IsValid(int minutes);
    static void Validate(int minutes);               // throws InvalidIntervalException
    static DateTime SnapStart(DateTime wallClock, int minutes); // floor down
    static DateTime SnapEnd(DateTime wallClock, int minutes);   // ceil up
}
```

`Validate` throws `InvalidIntervalException` for anything outside `{15, 30, 60}`
(including `0` and negatives). `SnapStart`/`SnapEnd` call `Validate` first so
`minutes = 0` can never cause a divide-by-zero in the tick arithmetic.

### D2 — Invalid interval cannot be constructed: `EventParams` validates in its constructor

`EventParams` changes from `readonly record struct(string? Timezone)` to a
manual `readonly record struct` with a validating constructor, so an invalid
interval can never be materialized anywhere in the system:

```
readonly record struct EventParams(string? Timezone, int IntervalMinutes)
{
    // constructor body calls Interval.Validate(intervalMinutes) then assigns both.
}
```

(Implemented as an explicit constructor with `init` properties so the
constructor body can validate; the `new EventParams(tz, n)` call sites are
unchanged in shape — the compiler enforces that every caller supplies an
interval.) The two construction sites:

- `Event.Create(...)` → `new EventParams(null, Interval.DefaultMinutes)` (REQ-002).
- `UpdateEventParams.Execute(...)` → `new EventParams(timezone, intervalMinutes)`
  (throws `InvalidIntervalException` on an out-of-set value, REQ-006).

`InvalidIntervalException` lives in `Common/` (like `InvalidTimezoneException`),
not under `Features/Events/Application/Exceptions/`, because `EventParams` is a
Domain type and must not depend on Application. Message mirrors the timezone
style: `"IntervalMinutes must be one of: 15, 30, 60."`.

### D3 — Snap grid: anchored at midnight of the wall-clock day, integer tick arithmetic

For a wall-clock bound `t` (Kind `Unspecified`, as produced by `ParseWallClock`)
and interval `m`:

```
ticksPerInterval = m * TimeSpan.TicksPerMinute
timeOfDayTicks  = t.TimeOfDay.Ticks            // [0, TicksPerDay)

SnapStart: t.Date + TimeSpan.FromTicks( (timeOfDayTicks / ticksPerInterval) * ticksPerInterval )
SnapEnd:   t.Date + TimeSpan.FromTicks( ((timeOfDayTicks + ticksPerInterval - 1) / ticksPerInterval) * ticksPerInterval )
```

- `start` floors to the previous boundary; `end` ceils to the next; a bound
  already exactly on a boundary is unchanged (the floor/ceil formulas are
  idempotent at exact multiples — REQ-012, SC-015).
- The grid is **anchored at `00:00:00` of the wall-clock date**. Because `15`,
  `30`, and `60` each divide both `60` and `1440`, this is identical to a
  "minutes-within-the-hour" formulation for the entire allowed set, so the
  choice is unambiguous. It also matches how a picker grid renders (00:00,
  00:30, …) and carries correctly across midnight (`23:59` on a 60-minute grid
  ceils to next-day `00:00`).
- Integer `TimeSpan.Ticks` arithmetic (no floating point) keeps the boundary
  test exact for second/sub-second components. The helper treats the input as
  wall-clock components regardless of `Kind` (specify `Unspecified` defensively,
  exactly as `TimezoneConverter.ToUtc` does), and the result is `Unspecified`.

### D4 — Snap in the submitter's wall-clock frame, before conversion, never reject misalignment

`SetAvailability` snaps each range's `start` down and `end` up **in the
submitter's timezone** before calling `TimezoneConverter.ToUtc`. Misalignment is
never a 400 — it is silently widened. This is what makes non-hour-offset zones
usable (an `Asia/Kolkata` `+05:30` submitter can submit a 60-minute-grid range
that lands on a `:30` UTC instant; REQ-017, SC-016). Snapping-then-converting
means the existing `ToUtc` gap/ambiguous rejection (REQ-016) and the existing
`AvailabilityFactory` invariants (REQ-014/015) automatically apply to the
**snapped** values with no changes to either.

### D5 — DB default vs domain default

The column default `30` (see the migration) is a **backfill + safety net only**.
Every application write supplies an explicit `IntervalMinutes` (`Event.Create`
→ 30; `UpdateEventParams` → the caller's value), so the domain default is
authoritative for new rows. The two constants must stay in sync: the column
default is `Interval.DefaultMinutes` and the DbContext mapping sets
`.HasDefaultValue(Interval.DefaultMinutes)` so they cannot drift.

## Changes

### Common (new + edited)

- **`Common/Interval.cs`** (new, `static class`) — the `D1` surface above.
- **`Common/InvalidIntervalException.cs`** (new) — `sealed class : Exception`,
  message `"IntervalMinutes must be one of: 15, 30, 60."`.
- **`Common/ExceptionHandler.cs`** — add one entry to the existing dictionary
  (no `if` chains): `InvalidIntervalException → (Status400BadRequest,
  "invalid_interval")`.

### Domain

- **`Features/Events/Domain/EventParams.cs`** — manual `readonly record struct`
  `EventParams(string? Timezone, int IntervalMinutes)` with a validating
  constructor (D2).
- **`Features/Events/Domain/Event.cs`** — only `Event.Create` changes: pass
  `new EventParams(null, Interval.DefaultMinutes)` (D2). `SetParams` and the
  `Params` property are unchanged.

### Persistence / write side

- **`Features/Events/Infra/DbContext.cs`** — extend the existing
  `entity.ComplexProperty(e => e.Params, c => …)` block:

  ```
  c.Property(p => p.Timezone).HasColumnName("timezone");
  c.Property(p => p.IntervalMinutes)
      .HasColumnName("interval_minutes")
      .HasDefaultValue(Interval.DefaultMinutes)   // → defaultValue: 30 in DDL
      .IsRequired();
  ```

  `.HasDefaultValue(30)` is **required** so the generated `AddColumn` carries
  `defaultValue: 30`; without it EF would emit `nullable: false` with no
  default and PostgreSQL would reject the migration on a non-empty `events`
  table.

- **Migration** — `dotnet ef migrations add AddEventIntervalMinutes` (naming
  follows the existing `<timestamp>_<PascalCase>` convention). Expected `Up()`:

  ```
  AddColumn<int>(name: "interval_minutes", table: "events",
                 type: "integer", nullable: false, defaultValue: 30);
  ```

  See "Migration / Rollout" for the snapshot-drift note — this migration also
  absorbs the latent 003 model change.

### Application

- **`Features/Events/Application/UpdateEventParams.cs`** — signature becomes
  `Execute(string publicId, Guid guestId, string? timezone, int intervalMinutes)`.
  Order (deterministic, satisfying SC-005 vs SC-009):

  1. `EventRepository.GetByPublicId` → `EventNotFoundException` (404).
  2. Organizer check → `ForbiddenException` (403).
  3. `var newParams = new EventParams(timezone, intervalMinutes);` — this
     constructor throws `InvalidIntervalException` (400 `invalid_interval`) for
     an out-of-set value, so interval is validated **before** the timezone.
  4. `if (timezone is not null) _timezoneConverter.Resolve(timezone);` —
     unchanged (`InvalidTimezoneException` → 400 `invalid_timezone`).
  5. `domainEvent.SetParams(newParams); Save;` return
     `new UpdateEventParamsResponse(timezone, intervalMinutes)`.

  The timezone path is otherwise unchanged.

- **`Features/Availability/Application/SetAvailability.cs`** — the interval is
  read from the event already loaded in this use case
  (`domainEvent.Params.IntervalMinutes`; `SetAvailability` already calls
  `EventRepository.GetByPublicId` at the top, so no new dependency or query is
  needed). Replace the range mapping with a snap step inserted between the
  wall-clock `r` and `ToUtc`:

  ```
  int intervalMinutes = domainEvent.Params.IntervalMinutes;
  … ranges.Select(r => _availabilityFactory.Create(pid,
        _timezoneConverter.ToUtc(Interval.SnapStart(r.Start, intervalMinutes), timezone),
        _timezoneConverter.ToUtc(Interval.SnapEnd(r.End, intervalMinutes),   timezone)))
  ```

  The response echo is **unchanged**: it already reconstructs wall-clock from the
  stored UTC instants via `ToWallClock(a.Start/End, timezone)`, so it
  automatically returns the snapped values (REQ-013) — no change to the return
  path. `AvailabilityFactory` invariants and the `ToUtc` DST gap/ambiguous check
  run on the snapped values exactly as today.

### API

- **`Features/Events/Api/UpdateEventParamsRequestParams.cs`** →
  `record UpdateEventParamsRequestParams(string? Timezone, int? IntervalMinutes)`.
  `IntervalMinutes` is **nullable** in the request DTO so a missing field can be
  distinguished from an invalid value: the endpoint emits the project-standard
  inline `Results.BadRequest` for the missing case.
- **`Features/Events/Api/UpdateEventParamsResponse.cs`** →
  `record(string? Timezone, int IntervalMinutes)`.
- **`Features/Events/Api/GetEventResponse.cs`** →
  `record(string PublicId, string Name, bool IsPasscodeProtected, string? Timezone, int IntervalMinutes)`
  (`IntervalMinutes` appended; JSON field order is irrelevant).
- **`Features/Events/Api/EventsRestService.cs`**:
  - `PUT /events/{publicId}/params` — after the existing `X-Guest-Id`
    validation, add the missing-field guard
    `if (requestParams.IntervalMinutes is null) return Results.BadRequest("intervalMinutes is required");`
    then pass `requestParams.IntervalMinutes.Value` to `Execute`. (REQ-007 /
    SC-006: missing → request-shape 400 with no error code, matching the join
    `timezone is required` convention.)
  - `GET /events/{publicId}` — construct
    `new GetEventResponse(dto.PublicId, dto.Name, dto.IsPasscodeProtected, dto.Timezone, dto.IntervalMinutes)`.
- **JSON casing** — minimal-API default (`JsonSerializerDefaults.Web`) is
  camelCase, confirmed by the existing hurl files using `publicId`,
  `isPasscodeProtected`, `timezone`. `IntervalMinutes` therefore serializes as
  **`intervalMinutes`** on the wire with no serializer configuration change.

### Read side (events)

- **`Features/Events/Infra/Read/get_event_by_public_id.sql`** — add the column to
  the SELECT (prefer editing the existing file; **no new SQL file**, so no
  csproj `<EmbeddedResource>` change):

  ```sql
  SELECT public_id, name, timezone, interval_minutes, (passcode_hash IS NOT NULL) AS is_passcode_protected
  FROM events
  WHERE public_id = @publicId
  ```

- **`Features/Events/Infra/Read/EventDto.cs`** →
  `record(string PublicId, string Name, string? Timezone, int IntervalMinutes, bool IsPasscodeProtected)`
  (field order tracks the SQL SELECT order — the Dapper mapping is ordinal-based).
- **`Features/Events/Infra/Read/ReadEventService.cs`** —
  `GetEventByPublicId` reads `reader.GetInt32(reader.GetOrdinal("interval_minutes"))`
  (non-null, no `IsDBNull` needed) and passes it into `EventDto`.
  `SearchEvents` and `EventSummaryDto`/`SearchEventsResponse` are **unchanged**
  (REQ-010: interval is not on the search/list contract).
- `GetParticipantAvailability` consumes `EventDto` for `Timezone` only and is
  unaffected by the added field.

### Availability read side

**No change.** `get_event_availabilities.sql` / `get_participant_availability.sql`
and their DTOs are untouched; the two `QUERY` read responses do not gain
`intervalMinutes` (REQ-010, SC-018).

### Composition

**No change.** `Interval` is static; `SetAvailability` reads the interval from
the already-loaded event; `UpdateEventParams` needs no new dependency. Neither
`Composition/ServiceCollectionExtensions.cs` nor `tests/…/Fixtures/IntegrationTest.cs`
is modified.

## Migration / Rollout

1. **Regenerate the snapshot absorbs latent 003 drift.** The committed
   `FindatimeDbContextModelSnapshot.cs` (and `20260920000223_AddAvailabilities.Designer.cs`)
   still model `Event.Timezone` as a flat scalar property, because change 003
   switched `DbContext.cs` to `ComplexProperty(e => e.Params, …)` **without
   generating a migration**. When 004 runs `dotnet ef migrations add
   AddEventIntervalMinutes`, EF diffs that stale snapshot against the live model,
   so the generated migration covers **both** (a) the new `interval_minutes`
   column and (b) `timezone` moving from scalar into the `Params` complex
   property. Because the column name `timezone` is unchanged, EF should emit **no
   SQL** for (b) — only the snapshot metadata updates. **Apply must verify** the
   generated `Up()` contains exactly one `AddColumn` for `interval_minutes`
   (with `defaultValue: 30`) and no `timezone` drop/add; if a spurious `timezone`
   operation appears, trim it (the column name is unchanged). After the add, the
   snapshot must model `Params` as a complex property carrying both `Timezone`
   and `IntervalMinutes`.

2. Apply migrations: `dotnet ef database update` (local and staging, per the
   AGENTS.md commands). Existing `events` rows get `interval_minutes = 30` from
   the column default; stored availability rows are untouched.

3. Build + test: `dotnet build` and `dotnet test`.

4. Regenerate the TypeScript client: `make typescript-client` (target exists).
   The regenerated models pick up `intervalMinutes` on `GetEventResponse` and the
   params request/response; the two `QUERY` availability reads remain absent from
   the generated service (pre-existing OpenAPI limitation) — the FE handoff doc
   calls this out.

5. **Breaking wire change, ship in lockstep.** `PUT /events/{publicId}/params`
   now requires `intervalMinutes`; clients sending only `{ timezone }` receive a
   `400`. Backend and regenerated TS client must deploy together.

6. Rollback: dropping the column restores the pre-change state; the domain
   change is the only behavioral addition and is exercised only through the new
   field, so an older client remains compatible with `GET /events/{publicId}`
   (extra JSON field) and `POST /events` (unchanged).

## Testing

### Unit tests (`tests/FindAtime.UnitTests/`)

- **`Common/IntervalTests.cs`** (new) — pure tests, no DI:
  - `Validate` accepts each of 15/30/60; throws `InvalidIntervalException` for
    `0`, `45`, `7`, `90`, `-30`.
  - `SnapStart` floors: `09:05`→`09:00`, `09:29`→`09:00`, `09:31`→`09:30` (30);
    `09:05`→`09:00`, `09:20`→`09:15` (15); `09:59`→`09:00` (60).
  - `SnapEnd` ceils: `09:05`→`09:30`, `09:50`→`10:00`, `09:29`→`09:30` (30);
    `09:05`→`09:15` (15); `09:59`→`10:00` (60).
  - Boundary-exact is unchanged for both directions (`09:00`, `09:30` on 30;
    `10:00` on 60); sub-minute components (`09:00:30` on 30 → start `09:00`,
    end `09:30`); cross-midnight ceil (`23:59` on 60 → next-day `00:00`).
- **`Domain/EventTests.cs`** (update):
  - `Create_ShouldDefaultParamsToNullTimezone` also asserts
    `IntervalMinutes == 30`.
  - `SetParams_ShouldReplaceParams` uses `new EventParams("Europe/Berlin", 15)`
    and `new EventParams(null, 60)` and asserts both fields each time.
  - New: constructing `new EventParams(null, 45)` throws
    `InvalidIntervalException` (can't-construct-invalid).
- **`Domain/EventFactoryTests.cs`** (optional) — assert a created event has
  `Params.IntervalMinutes == 30`.

### Integration tests (`tests/FindAtime.IntegrationTests/`)

- **`Application/Events/UpdateEventParamsTests.cs`** (update all existing
  `Execute` calls to pass an interval, e.g. `30`; add):
  - `Execute_ShouldPersistIntervalMinutes` (change to 15 → response 15 +
    `persisted.Params.IntervalMinutes == 15`).
  - `Execute_ShouldAcceptEachAllowedValue` (`[Theory]` over 15/30/60 → response
    echoes each).
  - `Execute_ShouldThrowForInvalidInterval` (45 → `InvalidIntervalException`).
  - Existing timezone/403/404 cases keep passing a valid interval.
- **`Application/Events/GetEventTests.cs`** (update):
  - `GetEventByPublicId_ShouldReturnEventDto` asserts `dto.IntervalMinutes == 30`
    (default on create).
- **`Application/Availability/SetAvailabilityTests.cs`** (add; set the desired
  interval first via `_updateEventParams.Execute(...)` where non-30 is needed):
  - `Execute_ShouldSnapMisalignedRangesAndEcho` — 30-min event, submit
    `09:05–09:50` → result echoes `09:00–10:00`, DB stores `09:00`/`10:00` UTC
    (or equivalent wall-clock→UTC in the test's `UTC` timezone).
  - `Execute_ShouldKeepAlignedRangesUnchanged` — `09:00–10:00` on 30 unchanged.
  - `Execute_ShouldSnapInWallClockFrameBeforeUtc` — `Asia/Kolkata` submitter,
    submit `09:05–09:50` on 30 → snap to `09:00–10:00` IST, stored UTC
    `03:30–04:30` (off-grid UTC, proving wall-clock-frame snapping).
  - `Execute_ShouldRejectSnappedRangeOver24Hours` — 60-min event, submit a
    range that is exactly 24h but whose outward ceil extends past 24h (e.g.
    `09:01` → next-day `09:01`) → `InvalidAvailabilityRangeException`.
  - Existing end-after-start and ≤24h cases remain valid (default 30).
- **`Application/Availability/GetEventAvailabilitiesTests.cs`** and
  **`GetParticipantAvailabilityTests.cs`** — update the four `UpdateEventParams.Execute`
  call sites to pass an interval (e.g. `30`); no behavior change.
- **`Fixtures/IntegrationTest.cs`** — no change (no new service).

Note: the missing-`intervalMinutes` 400 is an endpoint-level inline
`Results.BadRequest`; there are no HTTP-level integration tests in this repo, so
it is exercised via the hurl file rather than a use-case test.

## Hurl scratch files

- **`Features/Events/Api/events.hurl`** entry 4 (`PUT /events/{publicId}/params`):
  change the body to `{ "timezone": "Europe/Berlin", "intervalMinutes": 15 }` and
  add a response assertion (e.g. `jsonpath "$.intervalMinutes" == 15`) so the
  echo is checked. Update the entry's comment to note both fields are required.
- **`Features/Availability/Api/availability.hurl`** entry 6
  (`PUT …/availability`, default interval 30): add a misaligned range to
  demonstrate the snap and assert the echo. For example add
  `{ "start": "2026-09-21T18:05:00", "end": "2026-09-21T18:50:00" }` and assert
  `$.ranges[2].start == "2026-09-21T18:00:00"` and
  `$.ranges[2].end == "2026-09-21T19:00:00"`. Update the header's entry list to
  mention the snap case.

## Frontend handoff document (content outline)

`Docs/changes/004-events-time-intervals/frontend-handoff.md` is written during
apply (a task). Its contract-only content must cover, per spec REQ-020:

- `PUT /events/{publicId}/params` request `{ timezone, intervalMinutes }` and
  response `{ timezone, intervalMinutes }`; `intervalMinutes` REQUIRED and
  exactly 15/30/60.
- `GET /events/{publicId}` response now includes `intervalMinutes`.
- `POST /events` unchanged; new events get `intervalMinutes: 30`.
- Ranges are snapped `start` down / `end` up in the submitter's wall-clock
  timezone before conversion; the response echoes snapped values.
- A snapped range violating `end > start` or the 24-hour maximum is rejected
  `400 invalid_availability_range`, never clamped.
- Changing the interval does not retroactively re-align stored ranges.
- Complete error-code list for the touched endpoints, including
  `invalid_interval`.
- The two `QUERY` availability reads are absent from the generated TS client.

## Task breakdown guidance (for `tasks.md`, authored by sdd-tasks)

Suggested seams and ordering:

1. **Domain + Common + persistence** — `Common/Interval.cs`,
   `Common/InvalidIntervalException.cs`, `Common/ExceptionHandler.cs` entry,
   `EventParams` (validating ctor), `Event.Create` default, `DbContext`
   `IntervalMinutes` mapping, and the migration (with the snapshot-drift
   verification). *Depends on: nothing.*
2. **Application + API + read (events)** — `UpdateEventParams` signature/order,
   `UpdateEventParamsRequestParams`/`Response`, `GetEventResponse`,
   `EventsRestService` (missing-field guard + response mapping),
   `get_event_by_public_id.sql`, `EventDto`, `ReadEventService`.
   *Depends on: 1.*
3. **Availability snap** — `SetAvailability` snap step (reads the loaded event's
   interval). *Depends on: 1 (Interval + EventParams); can be parallel with 2.*
4. **Tests + hurl + client regen + handoff doc** — all unit/integration tests,
   both hurl files, `make typescript-client`, and
   `frontend-handoff.md`. *Depends on: 1, 2, 3.*

Each task should carry a build/`dotnet test` self-check per the SDD convention;
the migration task must explicitly verify the `Up()` body per the rollout note
above.

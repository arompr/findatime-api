# 004-events-time-intervals: Tasks

Landed: 2026-11-01T18:33:27Z

Ordered. Each task depends on the ones above it unless marked `[P]`
(parallel-safe). Each task ends with a `Verified: <iso>` stamp once its
**Verification** has passed. A fully landed change additionally carries a
`Landed: <iso>` line directly under its H1.

Sequencing notes:

- Tests ship with the task that owns the behaviour. `Interval`/`EventParams`
  unit tests land in Task 1, `UpdateEventParams`/`GetEvent` integration tests in
  Task 2, availability snap tests in Task 3, and wire-level artifacts (hurl, TS
  client, handoff doc) in Task 4.
- Task 1 changes the `EventParams` constructor arity, so it also carries a
  one-line interim fix at `UpdateEventParams.cs:26` (`new EventParams(timezone,
  Interval.DefaultMinutes)`) to keep the solution compiling; Task 2 replaces
  that with the caller-supplied value.
- Task 2 owns the `UpdateEventParams.Execute` signature change and therefore
  updates **every** call site, including `GetEventAvailabilitiesTests.cs` and
  `GetParticipantAvailabilityTests.cs` (design's Task-breakdown guidance folded
  them into Task 4; folding them into Task 2 keeps the tree green after each
  task). Task 3 is not `[P]` with Task 2 because its 60-minute snap cases set the
  event interval through `UpdateEventParams.Execute(…, 60)`.
- Migration/rollout steps that need a live database (`dotnet ef database
  update`, staging) stay in the design's rollout section; Task 1 verifies the
  generated migration and snapshot by inspection, which the container-free test
  run then exercises through `PostgresFixture`'s `MigrateAsync()`.

## Task 1 — Common `Interval`, validating `EventParams`, default 30, EF mapping + migration

**Depends on:** none

**Goal:** Introduce the single owner of the allowed interval set / default / snap
grid, make an out-of-set interval impossible to construct, default new events to
30, and add the `events.interval_minutes` column (default 30) whose migration
also absorbs the latent 003 snapshot drift.

**Work:**
- Create `src/FindAtime.Api/Common/Interval.cs` — `internal/public static class`
  with `const int DefaultMinutes = 30`, `Allowed = { 15, 30, 60 }`,
  `IsValid(int)`, `Validate(int)` (throws `InvalidIntervalException`), and
  `SnapStart(DateTime, int)` / `SnapEnd(DateTime, int)`. Snap arithmetic per
  design D3: integer `TimeSpan.Ticks` against the day anchored at `t.Date`
  (floor for start, ceil for end), `Validate` called first, result
  `DateTimeKind.Unspecified`.
- Create `src/FindAtime.Api/Common/InvalidIntervalException.cs` — `sealed class`
  deriving `Exception`, message `"IntervalMinutes must be one of: 15, 30, 60."`.
- Edit `src/FindAtime.Api/Common/ExceptionHandler.cs` — add
  `[typeof(InvalidIntervalException)] = (StatusCodes.Status400BadRequest, "invalid_interval")`
  to the existing `Mappings` dictionary (no `if` chain).
- Edit `src/FindAtime.Api/Features/Events/Domain/EventParams.cs` — replace the
  positional `readonly record struct EventParams(string? Timezone)` with a
  `readonly record struct EventParams` exposing `string? Timezone { get; init; }`
  and `int IntervalMinutes { get; init; }`, plus the explicit
  `EventParams(string? timezone, int intervalMinutes)` constructor that calls
  `Interval.Validate(intervalMinutes)` before assigning both.
- Edit `src/FindAtime.Api/Features/Events/Domain/Event.cs` — `Event.Create` passes
  `new EventParams(null, Interval.DefaultMinutes)`; `SetParams`/`Params` unchanged.
- Edit `src/FindAtime.Api/Features/Events/Application/UpdateEventParams.cs` —
  interim compile fix only: line 26 becomes
  `domainEvent.SetParams(new EventParams(timezone, Interval.DefaultMinutes));`
  (signature stays 3-arg; Task 2 replaces this).
- Edit `src/FindAtime.Api/Features/Events/Infra/DbContext.cs` — inside the
  existing `ComplexProperty(e => e.Params, c => …)` block keep the
  `timezone` mapping and add
  `c.Property(p => p.IntervalMinutes).HasColumnName("interval_minutes").HasDefaultValue(Interval.DefaultMinutes).IsRequired();`.
- Run `dotnet ef migrations add AddEventIntervalMinutes --project src/FindAtime.Api/FindAtime.Api.csproj`,
  producing `src/FindAtime.Api/Migrations/<timestamp>_AddEventIntervalMinutes.cs`
  (+ `.Designer.cs`) and an updated `FindatimeDbContextModelSnapshot.cs`. Confirm
  `Up()` contains exactly one `AddColumn<int>` for `interval_minutes` on `events`
  with `nullable: false, defaultValue: 30` and **no** `timezone` drop/add/
  `AlterColumn`; if EF emits a spurious `timezone` operation from the stale 003
  snapshot, trim it and re-check the snapshot. Do not hand-edit the migration
  beyond that trim.
- Create `tests/FindAtime.UnitTests/Common/IntervalTests.cs` — `Validate`
  accepts 15/30/60 and throws `InvalidIntervalException` for `0`, `7`, `45`,
  `90`, `-30`; `SnapStart` floors (`09:05`→`09:00`, `09:29`→`09:00`,
  `09:31`→`09:30` on 30; `09:20`→`09:15` on 15; `09:59`→`09:00` on 60);
  `SnapEnd` ceils (`09:05`→`09:30`, `09:50`→`10:00`, `09:29`→`09:30` on 30;
  `09:05`→`09:15` on 15; `09:59`→`10:00` on 60); exact bounds unchanged
  (`09:00`/`09:30` on 30, `10:00` on 60); sub-minute `09:00:30` on 30 →
  start `09:00` / end `09:30`; cross-midnight `23:59` on 60 → next-day `00:00`.
- Edit `tests/FindAtime.UnitTests/Domain/EventTests.cs` —
  `Create_ShouldDefaultParamsToNullTimezone` also asserts
  `Params.IntervalMinutes == 30`; `SetParams_ShouldReplaceParams` uses
  `new EventParams("Europe/Berlin", 15)` then `new EventParams(null, 60)` and
  asserts both fields each time; add a test that
  `new EventParams(null, 45)` throws `InvalidIntervalException`.
- Edit `tests/FindAtime.UnitTests/Domain/EventFactoryTests.cs` — assert a created
  event has `Params.IntervalMinutes == 30`.

**Acceptance criteria:**
- [x] `Interval.IsValid`/`Validate` accept exactly `15`/`30`/`60` and reject every other integer, including `0` and negatives, with `InvalidIntervalException` (REQ-001).
- [x] `Interval.SnapStart` floors and `Interval.SnapEnd` ceils on the day-anchored grid, are idempotent on exact boundaries, and ceil `23:59` on a 60-minute grid to the next day's `00:00` in wall-clock terms (REQ-011, REQ-012).
- [x] `new EventParams(tz, 45)` throws `InvalidIntervalException`; no invalid interval can be materialized (REQ-001, REQ-006).
- [x] `Event.Create` yields `Params.Timezone == null` and `Params.IntervalMinutes == 30` (REQ-002).
- [x] `InvalidIntervalException` is mapped to HTTP 400 with error code `invalid_interval` in `ExceptionHandler.Mappings` (REQ-006).
- [x] Migration `AddEventIntervalMinutes` adds a non-null `events.interval_minutes` integer column with `defaultValue: 30`, so pre-existing rows read back as 30 and no availability row is rewritten (REQ-003).
- [x] `FindatimeDbContextModelSnapshot.cs` models `Params` as a complex property carrying both `Timezone` and `IntervalMinutes`, and `has-pending-model-changes` reports none (REQ-003).

**Verification:**
- `dotnet build`
- `dotnet test tests/FindAtime.UnitTests/FindAtime.UnitTests.csproj --filter "IntervalTests|EventTests|EventFactoryTests"`
- `dotnet test` (integration run applies the new migration via `PostgresFixture`)
- `dotnet ef migrations has-pending-model-changes --project src/FindAtime.Api/Findatime.Api.csproj` → `No changes were found`
- `sed -n '1,60p' src/FindAtime.Api/Migrations/*_AddEventIntervalMinutes.cs` → exactly one `AddColumn<int>(name: "interval_minutes", table: "events", type: "integer", nullable: false, defaultValue: 30)`; no `timezone` drop/add/alter
- `grep -n -A14 'ComplexProperty' src/FindAtime.Api/Migrations/FindatimeDbContextModelSnapshot.cs` → the `Params` complex property block contains both `Timezone` and `IntervalMinutes` mapping to `timezone` / `interval_minutes`

Verified: 2026-11-01T18:19:58Z

## Task 2 — Events params application + API + event read side

**Depends on:** Task 1

**Goal:** Make `PUT /events/{publicId}/params` a full replace over
`timezone` + required `intervalMinutes` (missing → request-shape 400, out-of-set →
`invalid_interval`), and return `intervalMinutes` from
`GET /events/{publicId}` while leaving search/list untouched.

**Work:**
- Edit `src/FindAtime.Api/Features/Events/Application/UpdateEventParams.cs` —
  `Execute(string publicId, Guid guestId, string? timezone, int intervalMinutes)`;
  keep the order: load event (`EventNotFoundException`), organizer check
  (`ForbiddenException`), build `new EventParams(timezone, intervalMinutes)`
  (throws `InvalidIntervalException` before any timezone work), then
  `if (timezone is not null) _timezoneConverter.Resolve(timezone)`, then
  `SetParams` + `Save`, returning
  `new UpdateEventParamsResponse(timezone, intervalMinutes)`.
- Edit `src/FindAtime.Api/Features/Events/Api/UpdateEventParamsRequestParams.cs` —
  `public record UpdateEventParamsRequestParams(string? Timezone, int? IntervalMinutes);`
  (nullable so a missing field is distinguishable from an invalid value).
- Edit `src/FindAtime.Api/Features/Events/Api/UpdateEventParamsResponse.cs` —
  `public record UpdateEventParamsResponse(string? Timezone, int IntervalMinutes);`
- Edit `src/FindAtime.Api/Features/Events/Api/GetEventResponse.cs` —
  `public record GetEventResponse(string PublicId, string Name, bool IsPasscodeProtected, string? Timezone, int IntervalMinutes);`
- Edit `src/FindAtime.Api/Features/Events/Api/EventsRestService.cs`:
  - `PUT /events/{publicId}/params` — after the existing `X-Guest-Id` checks add
    `if (requestParams.IntervalMinutes is null) return Results.BadRequest("intervalMinutes is required");`
    and pass `requestParams.IntervalMinutes.Value` to `Execute`.
  - `GET /events/{publicId}` — pass `dto.IntervalMinutes` into the
    `GetEventResponse` construction.
- Edit `src/FindAtime.Api/Features/Events/Infra/Read/get_event_by_public_id.sql` —
  `SELECT public_id, name, timezone, interval_minutes, (passcode_hash IS NOT NULL) AS is_passcode_protected FROM events WHERE public_id = @publicId`
  (edit the existing file; no new SQL resource, no csproj change).
- Edit `src/FindAtime.Api/Features/Events/Infra/Read/EventDto.cs` —
  `public record EventDto(string PublicId, string Name, string? Timezone, int IntervalMinutes, bool IsPasscodeProtected);`
  (ordinal position follows the SELECT).
- Edit `src/FindAtime.Api/Features/Events/Infra/Read/ReadEventService.cs` —
  `GetEventByPublicId` reads
  `reader.GetInt32(reader.GetOrdinal("interval_minutes"))` (no `IsDBNull`) and
  passes it into `EventDto`; `SearchEvents` unchanged.
- Edit `tests/FindAtime.IntegrationTests/Application/Events/UpdateEventParamsTests.cs` —
  pass a valid interval (`30`) in every existing `Execute` call; add
  `Execute_ShouldPersistIntervalMinutes` (change to 15 → response
  `IntervalMinutes == 15` and persisted `Params.IntervalMinutes == 15`),
  `Execute_ShouldAcceptEachAllowedValue` (`[Theory]` over 15/30/60 → response
  echoes both fields), `Execute_ShouldThrowForInvalidInterval` (45 →
  `InvalidIntervalException`), and `Execute_ShouldValidateIntervalBeforeTimezone`
  (45 plus `"Not/A_Timezone"` → `InvalidIntervalException`), and add an
  `IntervalMinutes == 30` assertion to the existing "disable timezone" case.
- Edit `tests/FindAtime.IntegrationTests/Application/Events/GetEventTests.cs` —
  `GetEventByPublicId_ShouldReturnEventDto` asserts `dto.IntervalMinutes == 30`.
- Edit `tests/FindAtime.IntegrationTests/Application/Availability/GetEventAvailabilitiesTests.cs`
  and `tests/FindAtime.IntegrationTests/Application/Availability/GetParticipantAvailabilityTests.cs` —
  update their four `_updateEventParams.Execute(...)` call sites to pass `30`
  (no behaviour change).

**Acceptance criteria:**
- [x] A params update with each allowed value (15/30/60) returns 200 and echoes exactly the stored `timezone` and `intervalMinutes` (REQ-005, REQ-009).
- [x] An out-of-set `intervalMinutes` (e.g. 45) throws `InvalidIntervalException`, surfaced as 400 `invalid_interval` (REQ-006).
- [x] A request body without `intervalMinutes` returns the endpoint's inline request-shape 400 (`"intervalMinutes is required"`) before the use case runs (REQ-007).
- [x] Unknown timezone → 400 `invalid_timezone`, non-organizer → 403 `forbidden`, unknown event → 404 `event_not_found`, all unchanged (REQ-008).
- [x] `GET /events/{publicId}` returns `intervalMinutes` (30 for a freshly created event) alongside `publicId`, `name`, `isPasscodeProtected`, `timezone` (REQ-004).
- [x] `SearchEvents`/`EventSummaryDto`/`SearchEventsResponse` and the availability read SQL/DTOs gain no interval field (REQ-010).
- [x] Every `UpdateEventParams.Execute` call site in `tests/` compiles against the 4-argument signature and the full suite passes (REQ-005).

**Verification:**
- `dotnet build`
- `dotnet test tests/FindAtime.IntegrationTests/FindAtime.IntegrationTests.csproj --filter "UpdateEventParamsTests|GetEventTests"`
- `dotnet test`
- `grep -rn "IntervalMinutes" src/FindAtime.Api/Features/Events/Infra/Read/search_events.sql src/FindAtime.Api/Features/Events/Infra/Read/EventSummaryDto.cs src/FindAtime.Api/Features/Availability/Infra/Read/` → no matches

Verified: 2026-11-01T18:19:58Z

## Task 3 — Availability: snap submitted ranges on the event interval grid

**Depends on:** Task 1, Task 2

**Goal:** Snap each submitted range's `start` down and `end` up on the event's
interval grid in the submitter's wall-clock timezone before the existing
wall-clock → UTC conversion, so snapped values are stored and echoed, invariants
are enforced on the snapped values, and stored ranges are never re-aligned later.

**Work:**
- Edit `src/FindAtime.Api/Features/Availability/Application/SetAvailability.cs` —
  read `int intervalMinutes = domainEvent.Params.IntervalMinutes;` after the
  event is loaded (no new dependency/query) and change the range projection to
  `_availabilityFactory.Create(pid, _timezoneConverter.ToUtc(Interval.SnapStart(r.Start, intervalMinutes), timezone), _timezoneConverter.ToUtc(Interval.SnapEnd(r.End, intervalMinutes), timezone))`.
  Leave the response path (`ToWallClock` over the stored instants) untouched so
  the echo reflects the snapped values.
- Edit `tests/FindAtime.IntegrationTests/Application/Availability/SetAvailabilityTests.cs` —
  inject `UpdateEventParams` in `InitializeAsync` and add:
  - `Execute_ShouldSnapMisalignedRangesAndEcho` — 30-minute event, submit
    `09:05–09:50` in `UTC` → result echoes `09:00–10:00` and the persisted
    `Availability.Start/End` are the `09:00`/`10:00` UTC instants.
  - `Execute_ShouldKeepAlignedRangesUnchanged` — `09:00–10:00` on 30 is stored
    and echoed verbatim.
  - `Execute_ShouldSnapInWallClockFrameBeforeUtc` — `Asia/Kolkata` submitter,
    `09:05–09:50` on a 30-minute event → echo `09:00–10:00`, stored UTC
    `03:30–04:30Z` (off the 30-minute UTC grid).
  - `Execute_ShouldAcceptOffGridUtcForNonHourOffset` — set the event to 60 via
    `_updateEventParams.Execute(publicId, organizerGuestId, null, 60)`, submit
    `09:00–10:00` in `Asia/Kolkata` → accepted, stored `03:30–04:30Z`.
  - `Execute_ShouldRejectSnappedRangeOver24Hours` — 60-minute event, submit
    `09:01` → next-day `09:01` → snapped `09:00` → next-day `09:30` (24.5h) →
    `InvalidAvailabilityRangeException`, nothing persisted/clamped.
  - `Execute_ShouldRejectSnappedBoundInDstGap` — 30-minute event,
    `Europe/Berlin`, submit `2026-03-29T02:45:00`–`04:00:00` (snapped start
    `02:30` falls in the spring-forward gap) → `InvalidAvailabilityRangeException`.
  - `Execute_ShouldNotRealignStoredRangesWhenIntervalChanges` — store
    `09:05–09:50` on a 30-minute event, then update the interval to 60, then
    `GetParticipantAvailability` returns the stored `09:00–10:00` unchanged.
  - Keep the existing aligned cases passing (they stay valid under a 30 default).

**Acceptance criteria:**
- [x] A misaligned range is snapped `start` down / `end` up on the event's interval grid in the submitter's wall-clock timezone before UTC conversion (REQ-011).
- [x] An already-aligned bound is left byte-for-byte unchanged (REQ-012).
- [x] The snapped UTC instants are what gets stored, and the response echoes the snapped wall-clock values (REQ-013).
- [x] A snapped range violating `end > start` or the 24-hour maximum is rejected with 400 `invalid_availability_range` and is never clamped (REQ-014, REQ-018).
- [x] Snapping cannot turn a valid range into a zero-duration one (outward widening preserves `end > start`) (REQ-015).
- [x] A snapped bound that lands in a DST gap is rejected with 400 `invalid_availability_range`, exactly as before (REQ-016).
- [x] A 60-minute event accepts an `Asia/Kolkata` submitter whose stored UTC instants fall off the hour grid (REQ-017).
- [x] Changing the interval afterwards leaves stored ranges verbatim; availability reads do not re-align them (REQ-019).
- [x] Availability read result types are unchanged and carry no `intervalMinutes` (REQ-010).

**Verification:**
- `dotnet build`
- `dotnet test tests/FindAtime.IntegrationTests/FindAtime.IntegrationTests.csproj --filter "SetAvailabilityTests"`
- `dotnet test`

Verified: 2026-11-01T18:19:58Z

## Task 4 — Hurl updates, TypeScript client regeneration, frontend handoff document

**Depends on:** Task 1, Task 2, Task 3

**Goal:** Bring the wire-level artifacts and client in line with the new
contracts: the two hurl scratch files, the regenerated TypeScript client, and the
contract-only FE handoff document.

**Work:**
- Edit `src/FindAtime.Api/Features/Events/Api/events.hurl` — entry 4
  (`PUT /events/{publicId}/params`) body becomes
  `{ "timezone": "Europe/Berlin", "intervalMinutes": 15 }`, add an
  `[Asserts]` block with `jsonpath "$.intervalMinutes" == 15`, and update the
  entry comment plus the header entry list (line for entry 4) to say both fields
  are required; also assert `jsonpath "$.timezone" == "Europe/Berlin"`.
- Edit `src/FindAtime.Api/Features/Availability/Api/availability.hurl` — entry 6
  adds a third misaligned range
  `{ "start": "2026-09-21T18:05:00", "end": "2026-09-21T18:50:00" }` and an
  `[Asserts]` block asserting `jsonpath "$.ranges[2].start" == "2026-09-21T18:00:00"`
  and `jsonpath "$.ranges[2].end" == "2026-09-21T19:00:00"`; update the header
  entry list to mention the snap case.
- Run `make typescript-client` (builds Release + `npm install && npm run prepare`
  in `typescript-client`) and keep the regenerated
  `typescript-client/dist/generated/**` changes (`types.gen.d.ts`,
  `sdk.gen.d.ts`, `client.gen.d.ts` and siblings) in the change.
- Create `Docs/changes/004-events-time-intervals/frontend-handoff.md` —
  contract facts only (no component design), covering: (1) `PUT
  /events/{publicId}/params` request `{ timezone, intervalMinutes }` and
  response `{ timezone, intervalMinutes }`, with `intervalMinutes` REQUIRED and
  exactly 15/30/60; (2) the `GET /events/{publicId}` response now including
  `intervalMinutes`; (3) `POST /events` unchanged, new events get
  `intervalMinutes: 30`; (4) submitted ranges are snapped `start` down / `end`
  up in the submitter's wall-clock timezone before conversion and the response
  echoes the snapped values; (5) a snapped range violating `end > start` or the
  24-hour maximum is rejected with 400 `invalid_availability_range`, never
  clamped; (6) changing the interval does not retroactively re-align stored
  ranges; (7) the complete error-code list for the touched endpoints, including
  `invalid_interval`; (8) the two `QUERY` availability read endpoints are absent
  from the generated TypeScript client.

**Acceptance criteria:**
- [x] `events.hurl` entry 4 sends both `timezone` and `intervalMinutes` and asserts the echoed `intervalMinutes` (REQ-005, REQ-009).
- [x] `availability.hurl` demonstrates a snapped write and asserts the echoed snapped wall-clock range (REQ-013).
- [x] The regenerated TypeScript client exposes `intervalMinutes` on the `GetEventResponse` model and on the params request/response models, and no stale shapes remain (REQ-004, REQ-005, REQ-009).
- [x] `Docs/changes/004-events-time-intervals/frontend-handoff.md` exists and states all eight contract facts listed in the spec's "Frontend Handoff Document" section (REQ-020).

**Verification:**
- `dotnet build`
- `dotnet test`
- `make typescript-client`
- `grep -rn "intervalMinutes\|IntervalMinutes" typescript-client/dist/generated/types.gen.d.ts typescript-client/dist/generated/sdk.gen.d.ts` → present on `GetEventResponse` and the params request/response models
- With the API running (`podman compose up -d`, `dotnet ef database update --project src/FindAtime.Api/Findatime.Api.csproj`, `dotnet run --project src/FindAtime.Api/Findatime.Api.csproj --launch-profile http`):
  - `hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl`
  - `hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl`

Verified: 2026-11-01T18:19:58Z

---

**Fold target:** this change folds into BOTH `Docs/specs/events/` and
`Docs/specs/availability/`.

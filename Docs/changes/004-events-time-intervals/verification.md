# 004-events-time-intervals: Verification

**Status:** PASS

## Summary

All four tasks of change 004-events-time-intervals were independently verified against the change's spec, design, and tasks. The implementation builds cleanly with the .NET 11 SDK, the full test suite (96 unit + 76 integration tests) passes, the EF migration applies correctly, the two Hurl scratch files pass against a live API, and the TypeScript client types reflect the new contracts. No code was found outside the stated scope, and every REQ/SC acceptance criterion is traceable to implementation or tests.

## Task results

- Task 1 — Common `Interval`, validating `EventParams`, default 30, EF mapping + migration: **PASS** — `Interval`/`InvalidIntervalException` created, `EventParams` validates in its ctor, migration adds one non-null `interval_minutes` column with default 30, snapshot models `Params` with both fields, `has-pending-model-changes` reports none, unit tests pass.
- Task 2 — Events params application + API + event read side: **PASS** — `PUT /events/{publicId}/params` is a full replace over both fields with inline missing-field guard and `invalid_interval` mapping, `GET /events/{publicId}` returns `intervalMinutes`, search/list and availability read contracts remain unchanged, integration tests pass.
- Task 3 — Availability: snap submitted ranges on the event interval grid: **PASS** — `SetAvailability` snaps `start` down / `end` up in the submitter's wall-clock frame before `ToUtc`, snapped UTC instants are stored/echoed, invariants run on snapped values, DST/off-grid/24h-overlap cases tested and passing.
- Task 4 — Hurl updates, TypeScript client regeneration, frontend handoff document: **PASS** — both Hurl files pass live, `frontend-handoff.md` covers all eight contract facts, generated TS types contain `intervalMinutes` on `GetEventResponse` and the params request/response models. (Note: `typescript-client/dist/` and `src/generated/` are gitignored; the generated files are present locally with correct content.)

## Acceptance criteria

- [x] An event's interval is always exactly 15, 30, or 60 minutes. — Verified by `Interval.Allowed` set and `Interval.Validate` / `EventParams` ctor throwing for any other value (`IntervalTests`, `EventTests`).
- [x] A newly created event reports `intervalMinutes: 30` on `GET /events/{publicId}`. — Verified by `Event.Create` using `Interval.DefaultMinutes` (30) and `GetEventTests.GetEventByPublicId_ShouldReturnEventDto`.
- [x] An existing event created before this change reports `intervalMinutes: 30` and its stored ranges are untouched. — Verified by migration `AddEventIntervalMinutes` adding the column with `defaultValue: 30`; no data rewrite or backfill is performed.
- [x] `POST /events` request shape is unchanged. — Verified by inspecting `EventsRestService.MapEvents` POST handler: it accepts the same four fields and never reads an interval.
- [x] `GET /events/{publicId}` response includes `intervalMinutes`. — Verified by `GetEventResponse` and `EventsRestService` line 55; Hurl entry 3 also observes it.
- [x] `PUT /events/{publicId}/params` requires both `timezone` and `intervalMinutes`. — Verified by `UpdateEventParamsRequestParams` and the inline `intervalMinutes is required` guard in `EventsRestService` lines 74-75.
- [x] A params update with each allowed value (15/30/60) succeeds and echoes both `timezone` and `intervalMinutes`. — Verified by `UpdateEventParamsTests.Execute_ShouldAcceptEachAllowedValue` and Hurl entry 4.
- [x] A params update with an out-of-set interval returns 400 `invalid_interval`. — Verified by `UpdateEventParamsTests.Execute_ShouldThrowForInvalidInterval` and `ExceptionHandler` mapping.
- [x] A params update with missing `intervalMinutes` returns 400 (request-shape). — Verified by `EventsRestService` returning `Results.BadRequest("intervalMinutes is required")` before the use case runs.
- [x] A params update by a non-organizer returns 403 `forbidden`. — Verified by existing and updated `UpdateEventParamsTests.Execute_ShouldThrowForNonOrganizer`.
- [x] A params update for an unknown event returns 404 `event_not_found`. — Verified by `UpdateEventParamsTests.Execute_ShouldThrowForUnknownEvent`.
- [x] A params update with an unknown timezone returns 400 `invalid_timezone`. — Verified by `UpdateEventParamsTests.Execute_ShouldThrowForInvalidTimezone`; interval is validated first (`Execute_ShouldValidateIntervalBeforeTimezone`).
- [x] Availability ranges are snapped `start` down / `end` up on the event's interval grid in the submitter's wall-clock timezone, before UTC conversion. — Verified by `SetAvailability.cs` lines 48-53 and `SetAvailabilityTests.Execute_ShouldSnapMisalignedRangesAndEcho` / `Execute_ShouldSnapInWallClockFrameBeforeUtc`.
- [x] Already-aligned bounds are stored unchanged. — Verified by `Interval.SnapStart`/`SnapEnd` idempotence (`IntervalTests`) and `SetAvailabilityTests.Execute_ShouldKeepAlignedRangesUnchanged`.
- [x] Snapped values are both stored as UTC instants and echoed in the response. — Verified by `SetAvailabilityTests.Execute_ShouldSnapMisalignedRangesAndEcho` asserting DB UTC values and response wall-clock values.
- [x] A snapped range violating `end > start` or the 24-hour maximum is rejected with 400 `invalid_availability_range`, never clamped. — Verified by `SetAvailabilityTests.Execute_ShouldRejectSnappedRangeOver24Hours` and existing end-after-start tests; `AvailabilityFactory` runs on snapped UTC instants.
- [x] Snapping cannot produce a zero-duration range. — Verified by outward-widening semantics: `SnapStart` floors and `SnapEnd` ceils, so a valid range stays valid.
- [x] DST gap/ambiguous handling is applied to the snapped values. — Verified by `SetAvailabilityTests.Execute_ShouldRejectSnappedBoundInDstGap`.
- [x] A 60-minute event accepts a `+05:30` submitter whose UTC instants fall off the hour grid. — Verified by `SetAvailabilityTests.Execute_ShouldAcceptOffGridUtcForNonHourOffset`.
- [x] A range exactly 24 hours that snaps past 24 hours is rejected with 400 `invalid_availability_range`. — Verified by `SetAvailabilityTests.Execute_ShouldRejectSnappedRangeOver24Hours` (see Deviation Adjudication below).
- [x] Availability read shapes are unchanged and do not include `intervalMinutes`. — Verified by grep showing no `intervalMinutes` in `Features/Availability/Infra/Read/`, availability DTOs, or `EventSummaryDto`/`SearchEventsResponse`.
- [x] Changing the interval leaves stored ranges verbatim (no retroactive re-alignment). — Verified by `SetAvailabilityTests.Execute_ShouldNotRealignStoredRangesWhenIntervalChanges`.
- [x] `Docs/changes/004-events-time-intervals/frontend-handoff.md` exists and covers every item in the "Frontend Handoff Document" section. — Verified by reading the document; all eight bullets are present and contract-only.

## Spec conformance

| Requirement | Satisfied by | Evidence |
|-------------|--------------|----------|
| REQ-001 Closed set {15,30,60} | `Interval.Allowed`, `Interval.Validate`, `EventParams` ctor | `IntervalTests`, `EventTests.EventParams_ShouldThrowForInvalidInterval` |
| REQ-002 Default 30 on creation | `Event.Create` → `new EventParams(null, Interval.DefaultMinutes)` | `EventTests.Create_ShouldDefaultParamsToNullTimezone`, `EventFactoryTests.CreateEvent_ShouldDefaultParamsToInterval30` |
| REQ-003 Existing events report 30, ranges untouched | Migration `defaultValue: 30`; no semantic migration | `20261101173839_AddEventIntervalMinutes.cs` |
| REQ-004 `GET /events/{publicId}` returns `intervalMinutes` | `GetEventResponse`, `ReadEventService`, SQL SELECT | `GetEventTests.GetEventByPublicId_ShouldReturnEventDto` |
| REQ-005 Full replace requires both fields | `UpdateEventParamsRequestParams`, `UpdateEventParams.Execute` signature | Hurl entry 4 |
| REQ-006 Invalid interval → 400 `invalid_interval` | `ExceptionHandler.Mappings` + `InvalidIntervalException` | `UpdateEventParamsTests.Execute_ShouldThrowForInvalidInterval` |
| REQ-007 Missing `intervalMinutes` → request-shape 400 | Inline guard in `EventsRestService` lines 74-75 | Hurl/endpoint inspection |
| REQ-008 Existing failure behavior unchanged | Order: load → organizer → interval → timezone | `UpdateEventParamsTests` 403/404/invalid-timezone cases |
| REQ-009 Params response echoes both fields | `UpdateEventParamsResponse` | `UpdateEventParamsTests.Execute_ShouldAcceptEachAllowedValue` |
| REQ-010 Interval only on params/event-read | No interval in search/availability read DTOs/SQL | Grep result: no matches |
| REQ-011 Snap before UTC in submitter wall-clock frame | `SetAvailability.cs` lines 48-53 | `SetAvailabilityTests.Execute_ShouldSnapInWallClockFrameBeforeUtc` |
| REQ-012 Aligned bounds unchanged | Idempotent floor/ceil in `Interval.SnapStart`/`SnapEnd` | `IntervalTests.SnapStart_ShouldLeaveExactBoundaryUnchanged` etc. |
| REQ-013 Snapped values stored and echoed | `ToUtc(snap(...))` stored; response uses `ToWallClock` over stored instants | `SetAvailabilityTests.Execute_ShouldSnapMisalignedRangesAndEcho` |
| REQ-014 Invariants on snapped values, never clamped | `AvailabilityFactory.Create` receives snapped UTC instants | `SetAvailabilityTests.Execute_ShouldRejectSnappedRangeOver24Hours` |
| REQ-015 Snapping cannot create zero-duration | Outward widening preserves `end > start` | Provable from `Interval` arithmetic + existing end-after-start tests |
| REQ-016 DST gap/ambiguous on snapped values | `ToUtc` runs after snap | `SetAvailabilityTests.Execute_ShouldRejectSnappedBoundInDstGap` |
| REQ-017 Non-hour-offset timezones supported | Wall-clock snapping before UTC | `SetAvailabilityTests.Execute_ShouldAcceptOffGridUtcForNonHourOffset` |
| REQ-018 Exactly-24h range snapped over limit rejected | Snapped end ceils past 24h → factory rejects | `SetAvailabilityTests.Execute_ShouldRejectSnappedRangeOver24Hours` |
| REQ-019 No retroactive re-alignment | Reads return stored UTC verbatim | `SetAvailabilityTests.Execute_ShouldNotRealignStoredRangesWhenIntervalChanges` |
| REQ-020 Frontend handoff document | `frontend-handoff.md` covers all 8 contract facts | Document inspection |

### Scenario coverage

All 18 scenarios (SC-001 through SC-018) are covered by the implementation and the tests/Hurl files described above.

## Design conformance

- **D1** `Common/Interval` static class owns allowed set, default, and snap grid — followed exactly.
- **D2** `EventParams` validating constructor prevents invalid intervals — followed; `Event.Create` and `UpdateEventParams` construct through it.
- **D3** Day-anchored integer tick arithmetic for `SnapStart`/`SnapEnd` — followed; exact-boundary idempotence and cross-midnight ceil verified in unit tests.
- **D4** Snap in submitter wall-clock frame before `ToUtc`, never reject misalignment — followed in `SetAvailability`.
- **D5** DB default 30 matches domain default — followed; `DbContext` maps `.HasDefaultValue(Interval.DefaultMinutes)` and migration emits `defaultValue: 30`.

### Migration / rollout

- `Up()` contains exactly one `AddColumn<int>(name: "interval_minutes", table: "events", type: "integer", nullable: false, defaultValue: 30)`.
- No `timezone` drop/add/alter operations are present.
- Snapshot models `Params` as a complex property containing both `Timezone` and `IntervalMinutes`.
- `dotnet ef migrations has-pending-model-changes` reports: `No changes have been made to the model since the last migration.`

## Deviation adjudication (item C)

The Task 3 Work block's prose says the 60-minute over-24h case results in "09:00 → next-day 09:30 (24.5h)". That prose is arithmetically inconsistent: for inputs `09:01` → next-day `09:01` on a 60-minute grid, `SnapStart` floors to `09:00` and `SnapEnd` ceils to next-day `10:00`, producing a 25-hour range, not 24.5h.

The actual test code (`SetAvailabilityTests.Execute_ShouldRejectSnappedRangeOver24Hours`) uses:

```csharp
var start = new DateTime(2026, 9, 19, 9, 1, 0);
var end = start.AddHours(24); // 2026-09-20 09:01
```

on a 60-minute event. This snaps to `09:00` → next-day `10:00` (25h), which exceeds the 24-hour maximum and is rejected by `AvailabilityFactory` as `InvalidAvailabilityRangeException`, and the test asserts no rows are persisted. The test as written therefore cleanly proves REQ-018 / SC-014: a range that was ≤24h before snapping is pushed past 24h by outward rounding and is rejected without clamping. **Adjudication: passes.** The only issue is the imprecise wording in `tasks.md`, not the implementation or the test.

## Scope discipline

- `POST /events` is untouched.
- No new endpoint was added.
- `GET /events?guestId=` / `SearchEventsResponse` / `EventSummaryResponse` carry no interval field.
- The two `QUERY` availability read endpoints return unchanged shapes.
- No backfill or re-snap of existing availability data is performed.
- Existing events land on interval 30 only via the column default.

## Error mapping

`ExceptionHandler.Mappings` contains one new dictionary entry:

```csharp
[typeof(InvalidIntervalException)] = (StatusCodes.Status400BadRequest, "invalid_interval"),
```

No `if` chains were introduced.

## Frontend handoff document

`Docs/changes/004-events-time-intervals/frontend-handoff.md` exists and is contract-facts-only. It covers all eight required items:

1. `PUT /events/{publicId}/params` request/response shapes and allowed values.
2. `GET /events/{publicId}` response shape including `intervalMinutes`.
3. `POST /events` unchanged; new events default to 30.
4. Snapping behavior and echoed response.
5. Rejection of snapped ranges violating invariants (`400 invalid_availability_range`).
6. No retroactive re-alignment on interval change.
7. Complete error-code list for touched endpoints, including `invalid_interval`.
8. Note that the two `QUERY` availability reads are absent from the generated TypeScript client.

Spot-checks against code:
- `"intervalMinutes is required"` matches `EventsRestService.cs:75`.
- `"timezone is required"` matches `AvailabilityRestService.cs:115`.
- Error codes listed match `ExceptionHandler.Mappings`.

## Commands run

```bash
# Build
DOTNET_ROOT=/home/ar/.dotnet /home/ar/.dotnet/dotnet build
# Output: Build succeeded. 0 Warning(s), 0 Error(s).

# Full test suite
DOTNET_ROOT=/home/ar/.dotnet /home/ar/.dotnet/dotnet test
# Output: Unit 96 passed; Integration 76 passed.

# Filtered tests from tasks.md
DOTNET_ROOT=/home/ar/.dotnet /home/ar/.dotnet/dotnet test tests/FindAtime.UnitTests/FindAtime.UnitTests.csproj --filter "IntervalTests|EventTests|EventFactoryTests"
# Output: 45 passed.

DOTNET_ROOT=/home/ar/.dotnet /home/ar/.dotnet/dotnet test tests/FindAtime.IntegrationTests/FindAtime.IntegrationTests.csproj --filter "UpdateEventParamsTests|GetEventTests"
# Output: 14 passed.

DOTNET_ROOT=/home/ar/.dotnet /home/ar/.dotnet/dotnet test tests/FindAtime.IntegrationTests/FindAtime.IntegrationTests.csproj --filter "SetAvailabilityTests"
# Output: 17 passed.

# EF pending model changes
export DOTNET_ROOT=/home/ar/.dotnet; export PATH=/home/ar/.dotnet:$PATH; dotnet ef migrations has-pending-model-changes --project src/FindAtime.Api/FindAtime.Api.csproj
# Output: No changes have been made to the model since the last migration.

# Grep: interval absent from search/availability read contracts
grep -rn "IntervalMinutes\|intervalMinutes\|interval_minutes" src/FindAtime.Api/Features/Events/Infra/Read/search_events.sql src/FindAtime.Api/Features/Events/Infra/Read/EventSummaryDto.cs src/FindAtime.Api/Features/Availability/Infra/Read/
# Output: no matches.

# Grep: interval present on TS client models
grep -rn "intervalMinutes\|IntervalMinutes" typescript-client/dist/generated/types.gen.d.ts typescript-client/dist/generated/sdk.gen.d.ts
# Output: present on GetEventResponse (line 40), UpdateEventParamsRequestParams (line 85), UpdateEventParamsResponse (line 89).

# Live Hurl tests (after podman compose up -d + ef database update + dotnet run)
hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl
# Output: Success (6 requests).

hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl
# Output: Success (6 requests), including the snapped range assertion.
```

## Issues found

None. All tasks pass.

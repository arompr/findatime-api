# 006-availability-interval-projection: Verification

Independent check of the implementation against `spec.md`, `design.md` and
`tasks.md`.

Environment: `/home/ar/.dotnet` (SDK 11 RC) with
`PATH=/home/ar/.dotnet:$PATH DOTNET_ROOT=/home/ar/.dotnet`; PostgreSQL was
available for Testcontainers and for the Hurl run.

## Commands run

| Command | Result |
|---|---|
| `dotnet build` | 0 warnings, 0 errors |
| `dotnet test tests/FindAtime.UnitTests` (projection + interval filters) | 42 passed |
| `dotnet test tests/FindAtime.IntegrationTests --filter "Availability"` | 28 passed |
| `dotnet test` | 111 unit + 90 integration passed |
| `dotnet ef database update --no-build` | no migrations applied (none expected) |
| `hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl` against a local API | 9/9 requests passed |

## Requirement coverage

| Requirement | Evidence |
|---|---|
| REQ-001 reads use the current interval | `get_event_availabilities.sql` selects `interval_minutes`; both read use cases call `AvailabilityProjection.Project` with it. |
| REQ-002 projection is inward | `AvailabilityProjection` uses `Interval.Ceil(localStart)` / `Interval.Floor(localEnd)`; unit tests `Project_ShouldTrimBothMisalignedBoundsInward`, `Project_ShouldCeilHalfHourStartAndFloorEnd`. |
| REQ-003 ranges shorter than an interval disappear | Unit `Project_ShouldDropRangeWithoutFullInterval`; integration `Execute_ShouldOmitRangesWithoutFullInterval`; Hurl 11:30–12:30 disappears at 60. |
| REQ-004 aligned bounds unchanged | Unit `Project_ShouldLeaveAlignedRangeUnchanged`; existing integration tests seeded on the hour still pass. |
| REQ-005 union is projected | `Merge` runs before projection; unit `Project_ShouldMergeTouchingRangesBeforeProjecting`, `Project_ShouldMergeOverlappingRangesBeforeProjecting`. |
| REQ-006 event frame anchors the grid | Both use cases pass `eventTimezone ?? "UTC"`; unit `Project_ShouldAnchorGridOnEventTimezone`, integration `Execute_ShouldAnchorGridOnEventTimezoneForEveryViewer` (Kolkata). |
| REQ-007 viewer timezone does not change slots | Same integration test asserts UTC and Kolkata viewers receive the same instant (`03:30–04:30Z` vs `09:00–10:00`). |
| REQ-008 storage never rewritten | `SetAvailabilityTests.Execute_ShouldProjectStoredRangesOntoCurrentIntervalWhenRead` asserts the stored row is still `08:30–09:30`; `Execute_ShouldReturnStoredRangesWhenIntervalIsRestored` asserts 60 → 30 restores the half-hour view. No write-side file changed. |
| REQ-009 write path unchanged | `SetAvailability` and `Interval.SnapStart/SnapEnd` semantics are unchanged (delegating wrappers); the existing snap tests and the Hurl PUT entry (18:05–18:50 → 18:00–19:00) pass. |
| REQ-010 response shape unchanged | No response record edited; Hurl asserts the existing `timezone` / `participants` / `ranges` fields only. |
| REQ-011 both endpoints agree | Both use the same `AvailabilityProjection` call; Hurl asserts the same 09:00–10:00 / 18:00–19:00 output from the event-wide and single-participant reads. |
| REQ-012 all-projected-away participant still listed | Integration `Execute_ShouldKeepParticipantWhenAllRangesProjectAway`; Hurl asserts `participants[0].ranges` count 0 and `participants[1]` still present. |
| REQ-013 reads never fail on DST | Unit `Project_ShouldAdvanceSnappedStartAcrossSpringForwardGap`, `Project_ShouldKeepSnappedBoundsInwardAcrossFallBack`; `TimezoneConverter.ToUtc` (strict, write-side) untouched. |
| REQ-014 interval validation unchanged | `Interval.Validate` is still the single guard; unit `Project_ShouldThrowForDisallowedInterval`; `EventParams` untouched. |
| REQ-015 unrelated read behaviour unchanged | Existing availability/events read tests all pass; no endpoint, error mapping or exception changed. |

## Files changed

- `src/FindAtime.Api/Common/Interval.cs` — `Floor`/`Ceil`; `SnapStart`/`SnapEnd`
  delegate to them.
- `src/FindAtime.Api/Common/AvailabilityProjection.cs` — new projection helper
  (merge, inward snapping, lenient DST conversion).
- `src/FindAtime.Api/Features/Availability/Application/GetEventAvailabilities.cs`
  and `GetParticipantAvailability.cs` — project before rendering.
- `src/FindAtime.Api/Features/Availability/Infra/Read/ReadAvailabilityService.cs`
  and `EventAvailabilityDto.cs` — carry `interval_minutes`.
- `src/FindAtime.Api/Features/Availability/Infra/Read/get_event_availabilities.sql`
  — select `interval_minutes`.
- `src/FindAtime.Api/Composition/ServiceCollectionExtensions.cs` and
  `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` — register
  `AvailabilityProjection` as a singleton.
- Tests: new `tests/FindAtime.UnitTests/Common/AvailabilityProjectionTests.cs`;
  updated `SetAvailabilityTests`, `GetEventAvailabilitiesTests`,
  `GetParticipantAvailabilityTests`.
- `src/FindAtime.Api/Features/Availability/Api/availability.hurl` — interval
  switch and projection assertions.

## Deviations

- The design's fall-back test was implemented as an
  inward-invariant assertion (`start' >= start`, `end' <= end`) rather than a
  single hard-coded pair, because the exact instant chosen at a repeated
  boundary depends on the runtime's zone database.
- No migration and no TypeScript client regeneration, as designed.

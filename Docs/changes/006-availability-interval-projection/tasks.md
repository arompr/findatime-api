# 006-availability-interval-projection: Tasks

Landed: 2026-11-01T22:37:48Z

Ordered. Each task depends on the ones above it unless marked `[P]`
(parallel-safe). Each task ends with a `Verified: <iso>` stamp once its
**Verification** has passed. A fully landed change additionally carries a
`Landed: <iso>` line directly under its H1.

Environment:

- The solution targets `net11.0`; the SDK on `PATH` is 10, so build/test commands
  are prefixed with `DOTNET_ROOT=/home/ar/.dotnet`.
- Integration tests use Testcontainers PostgreSQL; `podman compose up -d` must be
  running.
- `hurl` is available for the wire scratch file.

Sequencing:

- Task 1 ships the pure arithmetic and its unit tests. Task 2 wires it into the
  read side with integration tests. Task 3 is the wire-level proof and depends on
  Task 2's endpoints behaving.
- No migration, no TypeScript client regeneration (response shapes unchanged).

## Task 1 — Inward grid primitives and the projection helper

**Depends on:** none

**Goal:** Make the inward grid arithmetic and the union/projection algorithm
available as pure, unit-testable code.

**Work:**
- Edit `src/FindAtime.Api/Common/Interval.cs`:
  - add `public static DateTime Floor(DateTime wallClock, int minutes)` with the
    current `SnapStart` body and
    `public static DateTime Ceil(DateTime wallClock, int minutes)` with the
    current `SnapEnd` body;
  - make `SnapStart`/`SnapEnd` delegate to them (write-side names stay).
- Create `src/FindAtime.Api/Common/AvailabilityProjection.cs`:
  - `public sealed class AvailabilityProjection` with a
    `TimezoneConverter` constructor dependency;
  - `Project(IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ranges, int intervalMinutes, string anchorTimezone)`
    returning `IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>`;
  - merge touching/overlapping ranges before projecting;
  - project `Interval.Ceil(localStart)` / `Interval.Floor(localEnd)` in the
    anchor zone and drop blocks with `end' <= start'`;
  - lenient DST conversion: gap → advance by the gap length; ambiguous → pick the
    interpretation that keeps the bound inward; otherwise use the zone offset.
- Create `tests/FindAtime.UnitTests/Common/AvailabilityProjectionTests.cs`
  covering the design's testing table (aligned, misaligned both ends, short
  range, `08:30–09:30` @60 → empty, `08:30–10:00` @60 → `09:00–10:00`,
  `08:20–10:40` @60 → `09:00–10:00`, touching union, overlapping union, unsorted
  input, cross-midnight, `+05:30`/`+05:45` anchors, DST spring-forward and
  fall-back, viewer-independent output).

**Acceptance criteria:**
- [x] `Interval.Floor` and `Interval.Ceil` exist and `SnapStart`/`SnapEnd`
      delegate to them without changing their behaviour (existing
      `IntervalTests` still pass).
- [x] `AvailabilityProjection.Project` merges the union first and emits only
      inward-aligned ranges.
- [x] A block with no full interval is dropped rather than widened or rejected.
- [x] The anchor zone controls the grid; no viewer frame is involved.
- [x] DST gap and ambiguous boundaries produce a result instead of throwing.
- [x] All new unit tests pass.

**Verification:**
- `DOTNET_ROOT=/home/ar/.dotnet dotnet build`
- `DOTNET_ROOT=/home/ar/.dotnet dotnet test tests/FindAtime.UnitTests/FindAtime.UnitTests.csproj --filter "AvailabilityProjectionTests|IntervalTests"`

Verified: 2026-11-01T22:37:48Z

## Task 2 — Read-side wiring and integration coverage

**Depends on:** Task 1

**Goal:** Make both availability read endpoints project stored ranges onto the
event's current interval.

**Work:**
- Edit `src/FindAtime.Api/Features/Availability/Infra/Read/get_event_availabilities.sql`
  to select `timezone, interval_minutes` in the first result set.
- Edit `EventAvailabilityDto` to carry `int IntervalMinutes`.
- Edit `ReadAvailabilityService.GetAllForEvent` to read `interval_minutes`.
- Edit `GetEventAvailabilities` and `GetParticipantAvailability`:
  - take `AvailabilityProjection` as a constructor dependency;
  - anchor on `eventTimezone ?? "UTC"`;
  - project each participant's ranges before the existing `ToWallClock`
    rendering.
- Register `AvailabilityProjection` as `Singleton` in
  `Composition/ServiceCollectionExtensions.cs` and in
  `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs`.
- Update the availability integration tests:
  - replace `SetAvailabilityTests.Execute_ShouldNotRealignStoredRangesWhenIntervalChanges`
    with a projection case (`08:30–09:30` at 30 → interval 60 → read is empty;
    rows still stored);
  - add cases to `GetEventAvailabilitiesTests` and `GetParticipantAvailabilityTests`
    for `08:30–10:00` @60 → `09:00–10:00`, aligned ranges unchanged,
    all-ranges-dropped participant still listed (event-wide only), timezone-off
    UTC anchor, and viewer-timezone-independent absolute ranges.

**Acceptance criteria:**
- [x] Both read endpoints project with the event's current interval.
- [x] `08:30–10:00` stored under interval 30 reads as `09:00–10:00` at 60.
- [x] `08:30–09:30` at 60 projects away, and the participant still appears in
      the event-wide read with an empty list.
- [x] Changing the interval modifies no stored row and changing it back restores
      the stored view.
- [x] The event timezone anchors the grid; the timezone option off anchors on
      UTC; viewer timezones do not change the absolute ranges.
- [x] Availability read response shapes are unchanged.
- [x] Existing availability and events tests keep passing.

**Verification:**
- `DOTNET_ROOT=/home/ar/.dotnet dotnet build`
- `DOTNET_ROOT=/home/ar/.dotnet dotnet test tests/FindAtime.IntegrationTests/FindAtime.IntegrationTests.csproj --filter "Availability"` → green
- `DOTNET_ROOT=/home/ar/.dotnet dotnet test` → full suite green

Verified: 2026-11-01T22:37:48Z

## Task 3 — `availability.hurl` wire entries

**Depends on:** Task 2

**Goal:** Prove the projection at the wire level, including an interval change.

**Work:**
- Extend `src/FindAtime.Api/Features/Availability/Api/availability.hurl`:
  - after the existing PUT, add a `PUT /events/{publicId}/params` entry setting
    `intervalMinutes` to 60 (keeping a valid timezone), using the organizer's
    `X-Guest-Id`;
  - add a `QUERY` for the single participant asserting the projected bodies for a
    range that loses half an hour and one that disappears;
  - add a `QUERY` for the event-wide read asserting the same projection and that
    the participant is still present;
  - update the entry list in the header comment to match.
- Keep the file runnable end to end (entries only depend on earlier captures).

**Acceptance criteria:**
- [x] The hurl workflow sets the interval to 60 after submitting availability.
- [x] A projected range (`08:30–10:00` → `09:00–10:00`) is asserted on both
      reads.
- [x] A fully projected-away range yields an empty range list for the
      participant.
- [x] The header entry list matches the entry order and methods.

**Verification:**
- `DOTNET_ROOT=/home/ar/.dotnet dotnet build`
- With the API running: `hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl`
  (or the grep-based ordering/shape check when no live API is feasible)

Verified: 2026-11-01T22:37:48Z

---

**Fold target:** `Docs/specs/availability/` — the read behavior, invariants,
requirements and design sections that currently promise verbatim reads.
`Docs/specs/events/` needs no delta (the interval param itself is unchanged).

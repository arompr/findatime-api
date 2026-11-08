# 006-availability-interval-projection: Design

## Summary

Introduce a stateless projection that turns a participant's stored UTC ranges
into interval-aligned UTC ranges at read time. Both availability read use cases
run it before rendering; nothing on the write side or in the database changes.

## Changes

### `Common/Interval`

Add the two inward primitives, implemented with the same tick arithmetic as the
existing outward ones:

```csharp
public static DateTime Floor(DateTime wallClock, int minutes)   // == SnapStart
public static DateTime Ceil(DateTime wallClock, int minutes)    // == SnapEnd
```

Keep `SnapStart`/`SnapEnd` as the write-side names (delegating to `Floor`/`Ceil`)
so `SetAvailability` and its tests are untouched. `Validate` keeps guarding all
four entry points.

### `Common/AvailabilityProjection`

New stateless helper. It is a `sealed class` with a `TimezoneConverter`
dependency (registered `Singleton`, like `TimezoneConverter` itself) so the
lenient DST conversion lives next to the strict one and both read use cases share
one code path.

```csharp
public sealed class AvailabilityProjection
{
    public AvailabilityProjection(TimezoneConverter timezoneConverter);

    public IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> Project(
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ranges,
        int intervalMinutes,
        string anchorTimezone);
}
```

Algorithm:

1. `Interval.Validate(intervalMinutes)`; resolve the anchor zone once.
2. **Merge** the ranges: order by start, collapse overlapping and touching
   ranges (`next.Start <= current.End`) into one union block.
3. For each block convert both bounds to wall-clock in the anchor zone, then
   `start' = Interval.Ceil(localStart)`, `end' = Interval.Floor(localEnd)`.
4. If `end' <= start'` the block contains no full interval — drop it.
5. Convert `start'`/`end'` back to instants (see DST below) and emit the pair.

Merging happens before projection deliberately: `08:30–09:30` + `09:30–10:30`
at interval 60 must yield `09:00–10:00`, which per-row projection would lose.

### DST handling in the projection

`TimezoneConverter.ToUtc` throws on gaps and ambiguous wall clocks, which is
right for writes and wrong for reads. The projection uses a private lenient
conversion:

- **Gap** (`zone.IsInvalidTime(local)`): advance the wall clock by the gap
  length (derived from the offsets either side of the transition) so the
  boundary lands on the first valid instant, which stays on the grid because
  real gaps are whole multiples of the interval in practice.
- **Ambiguous** (`zone.IsAmbiguousTime(local)`): choose the interpretation that
  keeps the projection inward — the earliest candidate at or after the original
  start, and the latest candidate at or before the original end. Fall back to
  the earliest/latest candidate if neither satisfies the bound.
- **Otherwise**: `zone.GetUtcOffset(local)` and construct the `DateTimeOffset`.

The strict `TimezoneConverter.ToUtc` is untouched, so `SetAvailability` keeps
rejecting gap/ambiguous submissions.

### Read side

- `get_event_availabilities.sql` — first result set becomes
  `SELECT timezone, interval_minutes FROM events WHERE public_id = @publicId;`.
  The second result set is unchanged.
- `EventAvailabilityDto` — gains `int IntervalMinutes` as the second member.
- `ReadAvailabilityService.GetAllForEvent` — reads the new column and passes it
  into the DTO.
- `GetEventAvailabilities.Execute` — anchor frame is `dto.EventTimezone ?? "UTC"`;
  each participant's stored ranges are passed through `Project(...)` before the
  existing `ToWallClock` rendering. Participants with no rows still appear with
  an empty list (unchanged LEFT JOIN behaviour).
- `GetParticipantAvailability.Execute` — already loads `EventDto` with
  `IntervalMinutes` and `Timezone`; same anchor rule and the same projection
  call before rendering.
- Both use cases gain the `AvailabilityProjection` constructor dependency.
- `Composition/ServiceCollectionExtensions.AddFindAtimeServices` and the
  integration-test fixture register `AvailabilityProjection` as `Singleton`.

The viewer frame stays exactly as it is (`viewer ?? event ?? UTC`, forced UTC
when the timezone option is off); only the grid anchor is pinned to the event
frame.

### Testing

- Unit (`FindAtime.UnitTests/Common/AvailabilityProjectionTests.cs`): table
  tests over the projection — aligned/misaligned starts and ends, ranges shorter
  than one interval, `08:30–09:30` → empty, `08:30–10:00` → `09:00–10:00`,
  `08:20–10:40` → `09:00–10:00`, union of touching ranges, union of overlapping
  ranges, unsorted input, cross-midnight, `+05:30`/`+05:45` anchors, DST
  spring-forward and fall-back boundaries, and viewer-independent output.
- Integration (`GetEventAvailabilitiesTests`, `GetParticipantAvailabilityTests`,
  `SetAvailabilityTests`): interval change 30 → 60 is reflected by both reads
  with rows untouched; changing back restores; all-ranges-dropped participant
  still listed; timezone-off event anchors on UTC; existing tests keep passing
  because their seeded ranges are already hour-aligned.
- Replace `SetAvailabilityTests.Execute_ShouldNotRealignStoredRangesWhenIntervalChanges`
  (whose seeded range happened to be aligned to 60 anyway) with an assertion
  that a `08:30–09:30` range stored under interval 30 disappears from a read
  after the interval becomes 60.

## Migration / Rollout

- No database migration and no data rewrite.
- No response-shape change, so no OpenAPI/TypeScript client regeneration.
- `availability.hurl` gains entries: set the interval to 60, read both endpoints,
  assert the projected bodies (including an all-dropped range); the header entry
  list is updated.
- Deployment is a normal API rollout. Old clients are unaffected because the
  response shapes are unchanged; they simply start seeing interval-aligned
  ranges.

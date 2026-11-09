# 008-availability-utc-instants: Design

## Summary

Move all availability wall-clock arithmetic onto the server, in the event's
frame, and let instants cross the wire unchanged. `AvailabilityProjection` gains
`SnapOutward`, the write-side mirror of `Project`. The use cases and DTOs drop
every timezone, the HTTP layer parses offset-bearing instants, and the reads
become `GET`.

## Changes

### Common

- `AvailabilityProjection.SnapOutward((Start, End) range, int intervalMinutes,
  string anchorTimezone)` → `(Start, End)` in UTC. It resolves the anchor,
  converts each bound to anchor wall clock with the existing private
  `ToWallClock`, applies `Interval.Floor` to the start and `Interval.Ceil` to
  the end, and converts back with the existing `ToInstant` (DST-lenient).
  `ToInstant` gets `notAfter = original start` for the start and
  `notBefore = original end` for the end, so on a repeated hour it picks the
  interpretation that keeps the range from shrinking.
- `Project` is unchanged.
- `Interval.SnapStart/SnapEnd` are deleted (no callers left); their unit tests
  are retargeted to `Floor`/`Ceil`, which they exercised anyway.
- `TimezoneConverter.ToUtc/ToWallClock` are deleted; `Resolve` stays (event
  params and join validation).

### Application

- `SetAvailability.Execute(publicId, participantId, guestId,
  IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> ranges)`. Anchor is
  `domainEvent.Params.Timezone ?? "UTC"`, the read side's rule. Per range:
  reject `End <= Start`, `SnapOutward`, `.ToUniversalTime()` (Npgsql
  `timestamptz` needs offset 0), then `AvailabilityFactory.Create` (24-hour
  check). `participant.SetTimezone(...)` is removed. Depends on
  `AvailabilityProjection` instead of `TimezoneConverter`.
- `GetEventAvailabilities.Execute(publicId)` and
  `GetParticipantAvailability.Execute(publicId, participantId)` lose the viewer
  timezone and the frame logic; they return `Project`'s output as UTC.
- Results: `Timezone` removed from `GetEventAvailabilitiesResult`,
  `ParticipantAvailabilityResult`, `SetAvailabilityResult`;
  `AvailabilityRangeResult(DateTimeOffset Start, DateTimeOffset End)`.

### HTTP

- Both reads: `MapMethods(..., [HttpMethods.Query])` → `MapGet`, no body, no
  timezone resolution. `AvailabilityQueryRequest` is deleted.
- PUT: `SetAvailabilityRequest(IReadOnlyList<AvailabilityRangeRequest> Ranges)`;
  the "timezone is required" check is removed. Bounds stay `string` in the
  request DTO so a bad value reaches our own 400 rather than a model-binding
  failure, and go through `ParseInstant`: `DateTime.TryParse(...,
  RoundtripKind)` to detect a missing offset (`Kind == Unspecified`), then
  `DateTimeOffset.TryParse(..., RoundtripKind)`, normalized with
  `ToUniversalTime()`. Failure throws `InvalidAvailabilityRangeException`
  ("must be an ISO 8601 instant with a UTC offset"), which the existing handler
  maps to 400 `invalid_availability_range`. `ParseInstant` is `public static` so
  it can be unit-tested directly.
- Responses drop `Timezone`; `AvailabilityRangeResponse(DateTimeOffset Start,
  DateTimeOffset End)` makes OpenAPI emit `format: date-time`. ASP.NET
  serializes them as `2026-11-03T14:00:00+00:00`.

### TypeScript client

`make typescript-client` now generates `getEventAvailabilities` and
`getParticipantAvailability` as GETs, so the hand-written `src/query.ts` (a
workaround for hey-api#2660) and its export are deleted, and `src/client.ts`
imports both from `generated/sdk.gen`. Published as 0.1.0 (breaking).

## Decisions

- **Outward on write, not nearest.** A misaligned range is widened on both ends
  (SC-002 → 13:00–15:00Z). Rounding to the nearest slot would silently move a
  participant's edges; widening matches the existing write semantics and the UI
  shows the saved result immediately.
- **No migration.** Pre-008 rows were snapped in the submitter's frame. `Project`
  already re-snaps inward in the event frame on every read, so old rows render
  correctly; at worst a half-hour-offset submitter's old row shows trimmed, as it
  did before.
- **Server tzdata still matters.** Snapping and projection use the container's
  tz rules. The API image (`mcr.microsoft.com/dotnet/aspnet:11.0`, Debian) ships
  tzdata from its base layer, so rebuild on a current base before deploying.

## Rollout

1. Merge the API, publish client 0.1.0, merge the UI with the dependency bump.
2. Deploy the API, then the UI right after. An old UI against the new API fails
   (QUERY → 405, wall-clock PUT → 400) until the UI deploys.
3. No data migration.
4. Check the API image ships a current tzdata.

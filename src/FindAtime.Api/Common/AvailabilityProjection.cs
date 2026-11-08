public sealed class AvailabilityProjection
{
    private readonly TimezoneConverter _timezoneConverter;

    public AvailabilityProjection(TimezoneConverter timezoneConverter)
    {
        _timezoneConverter = timezoneConverter;
    }

    public IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> Project(
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ranges,
        int intervalMinutes,
        string anchorTimezone)
    {
        Interval.Validate(intervalMinutes);

        TimeZoneInfo zone = _timezoneConverter.Resolve(anchorTimezone);

        var projected = new List<(DateTimeOffset Start, DateTimeOffset End)>();

        foreach (var block in Merge(ranges))
        {
            DateTime localStart = ToWallClock(zone, block.Start);
            DateTime localEnd = ToWallClock(zone, block.End);

            DateTime snappedStart = Interval.Ceil(localStart, intervalMinutes);
            DateTime snappedEnd = Interval.Floor(localEnd, intervalMinutes);

            if (snappedEnd <= snappedStart)
                continue;

            projected.Add((
                ToInstant(zone, snappedStart, block.Start, null),
                ToInstant(zone, snappedEnd, null, block.End)));
        }

        return projected;
    }

    /// <summary>
    /// Widens one instant range outward to the slot grid of the anchor timezone:
    /// the start is floored and the end is ceiled on the anchor's wall clock.
    /// Used on write so no selected time is lost. Returns UTC instants.
    /// </summary>
    public (DateTimeOffset Start, DateTimeOffset End) SnapOutward(
        (DateTimeOffset Start, DateTimeOffset End) range,
        int intervalMinutes,
        string anchorTimezone)
    {
        Interval.Validate(intervalMinutes);

        TimeZoneInfo zone = _timezoneConverter.Resolve(anchorTimezone);

        DateTime snappedStart = Interval.Floor(ToWallClock(zone, range.Start), intervalMinutes);
        DateTime snappedEnd = Interval.Ceil(ToWallClock(zone, range.End), intervalMinutes);

        return (
            ToInstant(zone, snappedStart, null, range.Start).ToUniversalTime(),
            ToInstant(zone, snappedEnd, range.End, null).ToUniversalTime());
    }

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> Merge(
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ranges)
    {
        var ordered = ranges
            .Where(range => range.End > range.Start)
            .OrderBy(range => range.Start)
            .ToList();

        var merged = new List<(DateTimeOffset Start, DateTimeOffset End)>();

        foreach (var range in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(range);
                continue;
            }

            var last = merged[^1];
            if (range.Start <= last.End)
            {
                merged[^1] = (last.Start, range.End > last.End ? range.End : last.End);
                continue;
            }

            merged.Add(range);
        }

        return merged;
    }

    private static DateTime ToWallClock(TimeZoneInfo zone, DateTimeOffset instant)
    {
        return DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTime(instant, zone).DateTime,
            DateTimeKind.Unspecified);
    }

    private static DateTimeOffset ToInstant(
        TimeZoneInfo zone,
        DateTime local,
        DateTimeOffset? notBefore,
        DateTimeOffset? notAfter)
    {
        if (zone.IsInvalidTime(local))
            local = local.Add(GapLength(zone, local));

        if (zone.IsAmbiguousTime(local))
        {
            var candidates = zone.GetAmbiguousTimeOffsets(local)
                .Select(offset => new DateTimeOffset(local, offset))
                .OrderBy(candidate => candidate.UtcDateTime)
                .ToList();

            if (notBefore is not null)
            {
                var after = candidates.FirstOrDefault(candidate => candidate >= notBefore.Value);
                return after == default ? candidates[^1] : after;
            }

            if (notAfter is not null)
            {
                var before = candidates.LastOrDefault(candidate => candidate <= notAfter.Value);
                return before == default ? candidates[0] : before;
            }

            return candidates[0];
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static TimeSpan GapLength(TimeZoneInfo zone, DateTime local)
    {
        TimeSpan before = zone.GetUtcOffset(local.AddHours(-2));
        TimeSpan after = zone.GetUtcOffset(local.AddHours(2));

        return after - before;
    }
}

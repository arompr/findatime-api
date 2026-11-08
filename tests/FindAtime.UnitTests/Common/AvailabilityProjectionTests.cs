namespace FindAtime.UnitTests.Common;

public class AvailabilityProjectionTests
{
    private static readonly AvailabilityProjection Projection = new(new TimezoneConverter());

    private static (DateTimeOffset Start, DateTimeOffset End) Utc(
        int day, int hour, int minute, int endHour, int endMinute)
    {
        return (
            new DateTimeOffset(2026, 9, day, hour, minute, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, day, endHour, endMinute, 0, TimeSpan.Zero));
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Range(
        DateTimeOffset start, DateTimeOffset end) => (start, end);

    private static IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> Project(
        int intervalMinutes,
        string anchorTimezone,
        params (DateTimeOffset Start, DateTimeOffset End)[] ranges)
        => Projection.Project(ranges, intervalMinutes, anchorTimezone);

    [Fact]
    public void Project_ShouldLeaveAlignedRangeUnchanged()
    {
        var result = Project(60, "UTC", Utc(19, 9, 0, 10, 0));

        var range = Assert.Single(result);
        Assert.Equal(Utc(19, 9, 0, 10, 0), range);
    }

    [Fact]
    public void Project_ShouldDropRangeWithoutFullInterval()
    {
        var result = Project(60, "UTC", Utc(19, 8, 30, 9, 30));

        Assert.Empty(result);
    }

    [Fact]
    public void Project_ShouldCeilHalfHourStartAndFloorEnd()
    {
        var result = Project(60, "UTC", Utc(19, 8, 30, 10, 0));

        var range = Assert.Single(result);
        Assert.Equal(Utc(19, 9, 0, 10, 0), range);
    }

    [Fact]
    public void Project_ShouldTrimBothMisalignedBoundsInward()
    {
        var result = Project(60, "UTC", Utc(19, 8, 20, 10, 40));

        var range = Assert.Single(result);
        Assert.Equal(Utc(19, 9, 0, 10, 0), range);
    }

    [Fact]
    public void Project_ShouldMergeTouchingRangesBeforeProjecting()
    {
        var result = Project(
            60,
            "UTC",
            Utc(19, 8, 30, 9, 30),
            Utc(19, 9, 30, 10, 30));

        var range = Assert.Single(result);
        Assert.Equal(Utc(19, 9, 0, 10, 0), range);
    }

    [Fact]
    public void Project_ShouldMergeOverlappingRangesBeforeProjecting()
    {
        var result = Project(
            60,
            "UTC",
            Utc(19, 8, 0, 9, 30),
            Utc(19, 9, 0, 10, 30));

        var range = Assert.Single(result);
        Assert.Equal(Utc(19, 8, 0, 10, 0), range);
    }

    [Fact]
    public void Project_ShouldOrderUnsortedRanges()
    {
        var result = Project(
            60,
            "UTC",
            Utc(19, 14, 0, 15, 0),
            Utc(19, 9, 0, 10, 0));

        Assert.Equal(2, result.Count);
        Assert.Equal(Utc(19, 9, 0, 10, 0), result[0]);
        Assert.Equal(Utc(19, 14, 0, 15, 0), result[1]);
    }

    [Fact]
    public void Project_ShouldProjectAcrossMidnight()
    {
        var start = new DateTimeOffset(2026, 9, 19, 22, 30, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 20, 0, 40, 0, TimeSpan.Zero);

        var result = Project(60, "UTC", Range(start, end));

        var range = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 23, 0, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero), range.End);
    }

    [Fact]
    public void Project_ShouldAnchorGridOnEventTimezone()
    {
        var result = Project(60, "Asia/Kolkata", Utc(19, 3, 0, 4, 30));

        var range = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 3, 30, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 4, 30, 0, TimeSpan.Zero), range.End);
    }

    [Fact]
    public void Project_ShouldSupportQuarterHourOffsetAnchors()
    {
        var result = Project(60, "Asia/Kathmandu", Utc(19, 2, 45, 4, 30));

        var range = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 3, 15, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 4, 15, 0, TimeSpan.Zero), range.End);
    }

    [Fact]
    public void Project_ShouldApplyFifteenMinuteGrid()
    {
        var result = Project(60, "UTC", Utc(19, 9, 0, 11, 0));
        Assert.Equal(Utc(19, 9, 0, 11, 0), Assert.Single(result));

        var fifteen = Project(15, "UTC", Utc(19, 9, 5, 9, 40));
        var range = Assert.Single(fifteen);
        Assert.Equal(Utc(19, 9, 15, 9, 30), range);
    }

    [Fact]
    public void Project_ShouldAdvanceSnappedStartAcrossSpringForwardGap()
    {
        var start = new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.Zero);

        var result = Project(60, "Europe/Berlin", Range(start, end));

        var range = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 2, 0, 0, TimeSpan.Zero), range.End);
    }

    [Fact]
    public void Project_ShouldKeepSnappedBoundsInwardAcrossFallBack()
    {
        var start = new DateTimeOffset(2026, 10, 24, 23, 45, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.Zero);

        var result = Project(60, "Europe/Berlin", Range(start, end));

        var range = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero), range.End);
        Assert.True(range.Start >= start);
        Assert.True(range.End <= end);
    }

    [Fact]
    public void Project_ShouldThrowForDisallowedInterval()
    {
        Assert.Throws<InvalidIntervalException>(() => Project(45, "UTC", Utc(19, 9, 0, 10, 0)));
    }

    [Fact]
    public void Project_ShouldReturnEmptyForNoRanges()
    {
        Assert.Empty(Project(60, "UTC"));
    }

    private const string NewYork = "America/New_York";

    private static (DateTimeOffset Start, DateTimeOffset End) Snap(
        int intervalMinutes,
        string anchorTimezone,
        (DateTimeOffset Start, DateTimeOffset End) range)
        => Projection.SnapOutward(range, intervalMinutes, anchorTimezone);

    [Fact]
    public void SnapOutward_ShouldWidenHalfHourOffsetViewerRangeOntoAnchorGrid()
    {
        // A Kolkata viewer's 19:00-20:00 IST is 13:30-14:30Z, i.e. 09:30-10:30 EDT.
        var snapped = Snap(60, NewYork, Utc(19, 13, 30, 14, 30));

        Assert.Equal(Utc(19, 13, 0, 15, 0), snapped);
        Assert.Equal(TimeSpan.Zero, snapped.Start.Offset);
        Assert.Equal(TimeSpan.Zero, snapped.End.Offset);

        // Reading it back on the same grid keeps it (09:00-11:00 EDT), never empty.
        var projected = Assert.Single(Project(60, NewYork, snapped));
        Assert.Equal(Utc(19, 13, 0, 15, 0), projected);
    }

    [Fact]
    public void SnapOutward_ShouldLeaveAlignedRangeUnchanged()
    {
        var aligned = (
            new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 3, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal(aligned, Snap(30, NewYork, aligned));
    }

    [Fact]
    public void SnapOutward_ShouldNormalizeNonUtcOffsetToUtc()
    {
        var start = new DateTimeOffset(2026, 9, 19, 19, 5, 0, TimeSpan.FromHours(5.5));
        var end = new DateTimeOffset(2026, 9, 19, 19, 50, 0, TimeSpan.FromHours(5.5));

        var snapped = Snap(60, NewYork, (start, end));

        Assert.Equal(Utc(19, 13, 0, 15, 0), snapped);
        Assert.Equal(TimeSpan.Zero, snapped.Start.Offset);
    }

    [Fact]
    public void SnapOutward_ShouldUseUtcGridForUtcAnchor()
    {
        var snapped = Snap(60, "UTC", Utc(19, 13, 30, 14, 30));

        Assert.Equal(Utc(19, 13, 0, 15, 0), snapped);
        Assert.Equal(Utc(19, 9, 0, 10, 0), Snap(30, "UTC", Utc(19, 9, 5, 9, 50)));
    }

    [Fact]
    public void SnapOutward_ShouldSnapAcrossNewYorkFallBack()
    {
        // Nov 1 2026: 01:00-02:00 EDT then 01:00-02:00 EST. 05:20Z is 01:20 EDT and
        // 06:40Z is 01:40 EST; the floor keeps the EDT 01:00 and the ceil picks the
        // EST 02:00 so the range only grows.
        var start = new DateTimeOffset(2026, 11, 1, 5, 20, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 1, 6, 40, 0, TimeSpan.Zero);

        var snapped = Snap(60, NewYork, (start, end));

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero), snapped.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero), snapped.End);
        Assert.True(snapped.Start <= start);
        Assert.True(snapped.End >= end);
    }

    [Fact]
    public void SnapOutward_ShouldSnapRangeSpanningNewYorkDstChange()
    {
        // Oct 31 23:15 EDT (03:15Z) to Nov 1 09:45 EST (14:45Z).
        var start = new DateTimeOffset(2026, 11, 1, 3, 15, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 1, 14, 45, 0, TimeSpan.Zero);

        var snapped = Snap(60, NewYork, (start, end));

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero), snapped.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 15, 0, 0, TimeSpan.Zero), snapped.End);
    }

    [Fact]
    public void SnapOutward_ShouldThrowForDisallowedInterval()
    {
        Assert.Throws<InvalidIntervalException>(() => Snap(45, "UTC", Utc(19, 9, 0, 10, 0)));
    }
}

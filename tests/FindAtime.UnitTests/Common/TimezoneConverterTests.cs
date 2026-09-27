namespace FindAtime.UnitTests.Common;

public class TimezoneConverterTests
{
    private const string NewYork = "America/New_York";
    private const string Tokyo = "Asia/Tokyo";

    private readonly TimezoneConverter _converter = new();

    [Fact]
    public void Resolve_ShouldReturnZoneForValidIanaId()
    {
        var zone = _converter.Resolve(NewYork);

        Assert.Equal(NewYork, zone.Id);
    }

    [Fact]
    public void Resolve_ShouldThrowForUnknownTimezone()
    {
        Assert.Throws<InvalidTimezoneException>(
            () => _converter.Resolve("Not/A_Timezone"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ShouldThrowForBlankTimezone(string ianaId)
    {
        Assert.Throws<InvalidTimezoneException>(
            () => _converter.Resolve(ianaId));
    }

    [Fact]
    public void ToUtc_ShouldConvertWallClockToUtcInstant()
    {
        var wallClock = new DateTime(2024, 1, 15, 12, 0, 0);

        var utc = _converter.ToUtc(wallClock, NewYork);

        Assert.Equal(new DateTimeOffset(2024, 1, 15, 17, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_ShouldAccountForDaylightSavingTime()
    {
        var wallClock = new DateTime(2024, 7, 15, 12, 0, 0);

        var utc = _converter.ToUtc(wallClock, NewYork);

        Assert.Equal(new DateTimeOffset(2024, 7, 15, 16, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_ShouldHandleTimezoneAheadOfUtc()
    {
        var wallClock = new DateTime(2024, 1, 15, 12, 0, 0);

        var utc = _converter.ToUtc(wallClock, Tokyo);

        Assert.Equal(new DateTimeOffset(2024, 1, 15, 3, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_ShouldTreatUtcKindInputAsWallClock()
    {
        var wallClock = DateTime.SpecifyKind(
            new DateTime(2024, 1, 15, 12, 0, 0), DateTimeKind.Utc);

        var utc = _converter.ToUtc(wallClock, NewYork);

        Assert.Equal(new DateTimeOffset(2024, 1, 15, 17, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_ShouldThrowForInvalidTimezone()
    {
        Assert.Throws<InvalidTimezoneException>(
            () => _converter.ToUtc(new DateTime(2024, 1, 15, 12, 0, 0), "Not/A_Timezone"));
    }

    [Fact]
    public void ToUtc_ShouldThrowForNonexistentWallClockDuringSpringForward()
    {
        var gap = new DateTime(2024, 3, 10, 2, 30, 0);

        Assert.Throws<InvalidAvailabilityRangeException>(
            () => _converter.ToUtc(gap, NewYork));
    }

    [Fact]
    public void ToUtc_ShouldThrowForAmbiguousWallClockDuringFallBack()
    {
        var repeated = new DateTime(2024, 11, 3, 1, 30, 0);

        Assert.Throws<InvalidAvailabilityRangeException>(
            () => _converter.ToUtc(repeated, NewYork));
    }

    [Fact]
    public void ToWallClock_ShouldConvertUtcInstantToLocalWallClock()
    {
        var instant = new DateTimeOffset(2024, 1, 15, 17, 0, 0, TimeSpan.Zero);

        var wallClock = _converter.ToWallClock(instant, NewYork);

        Assert.Equal(new DateTime(2024, 1, 15, 12, 0, 0), wallClock);
    }

    [Fact]
    public void ToWallClock_ShouldReturnUnspecifiedKind()
    {
        var instant = new DateTimeOffset(2024, 1, 15, 17, 0, 0, TimeSpan.Zero);

        var wallClock = _converter.ToWallClock(instant, NewYork);

        Assert.Equal(DateTimeKind.Unspecified, wallClock.Kind);
    }

    [Fact]
    public void ToWallClock_ShouldThrowForInvalidTimezone()
    {
        var instant = new DateTimeOffset(2024, 1, 15, 17, 0, 0, TimeSpan.Zero);

        Assert.Throws<InvalidTimezoneException>(
            () => _converter.ToWallClock(instant, "Not/A_Timezone"));
    }

    [Fact]
    public void ToUtcAndToWallClock_ShouldRoundTrip()
    {
        var wallClock = new DateTime(2024, 7, 15, 12, 0, 0);

        var utc = _converter.ToUtc(wallClock, NewYork);
        var result = _converter.ToWallClock(utc, NewYork);

        Assert.Equal(wallClock, result);
    }
}

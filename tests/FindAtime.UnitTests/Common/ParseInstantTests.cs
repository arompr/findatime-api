namespace FindAtime.UnitTests.Common;

public class ParseInstantTests
{
    [Theory]
    [InlineData("2026-11-03T14:00:00Z")]
    [InlineData("2026-11-03T14:00:00+00:00")]
    [InlineData("2026-11-03T09:00:00-05:00")]
    [InlineData("2026-11-03T19:30:00+05:30")]
    public void ParseInstant_ShouldAcceptExplicitOffsetsAndReturnUtc(string value)
    {
        var parsed = AvailabilityRestService.ParseInstant(value);

        Assert.Equal(new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.Zero), parsed);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }

    [Theory]
    [InlineData("2026-11-03T14:00:00")]
    [InlineData("2026-11-03")]
    [InlineData("not a date")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseInstant_ShouldRejectValuesWithoutOffset(string? value)
    {
        Assert.Throws<InvalidAvailabilityRangeException>(() => AvailabilityRestService.ParseInstant(value));
    }
}

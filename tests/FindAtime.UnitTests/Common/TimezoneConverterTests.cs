namespace FindAtime.UnitTests.Common;

public class TimezoneConverterTests
{
    private const string NewYork = "America/New_York";

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
}

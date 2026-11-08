namespace FindAtime.UnitTests.Common;

public class IntervalTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    public void Validate_ShouldAcceptAllowedValues(int minutes)
    {
        Interval.Validate(minutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(-30)]
    public void Validate_ShouldThrowForDisallowedValues(int minutes)
    {
        Assert.Throws<InvalidIntervalException>(() => Interval.Validate(minutes));
    }

    [Theory]
    [InlineData(30, 9, 5, 9, 0)]
    [InlineData(30, 9, 29, 9, 0)]
    [InlineData(30, 9, 31, 9, 30)]
    [InlineData(15, 9, 20, 9, 15)]
    [InlineData(60, 9, 59, 9, 0)]
    public void Floor_ShouldFloorToPreviousBoundary(
        int minutes, int inHour, int inMinute, int outHour, int outMinute)
    {
        var input = new DateTime(2024, 1, 15, inHour, inMinute, 0);
        var expected = new DateTime(2024, 1, 15, outHour, outMinute, 0);

        var result = Interval.Floor(input, minutes);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(30, 9, 5, 9, 30)]
    [InlineData(30, 9, 50, 10, 0)]
    [InlineData(30, 9, 29, 9, 30)]
    [InlineData(15, 9, 5, 9, 15)]
    [InlineData(60, 9, 59, 10, 0)]
    public void Ceil_ShouldCeilToNextBoundary(
        int minutes, int inHour, int inMinute, int outHour, int outMinute)
    {
        var input = new DateTime(2024, 1, 15, inHour, inMinute, 0);
        var expected = new DateTime(2024, 1, 15, outHour, outMinute, 0);

        var result = Interval.Ceil(input, minutes);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(30, 9, 0)]
    [InlineData(30, 9, 30)]
    [InlineData(60, 10, 0)]
    public void Floor_ShouldLeaveExactBoundaryUnchanged(int minutes, int hour, int minute)
    {
        var input = new DateTime(2024, 1, 15, hour, minute, 0);

        var result = Interval.Floor(input, minutes);

        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(30, 9, 0)]
    [InlineData(30, 9, 30)]
    [InlineData(60, 10, 0)]
    public void Ceil_ShouldLeaveExactBoundaryUnchanged(int minutes, int hour, int minute)
    {
        var input = new DateTime(2024, 1, 15, hour, minute, 0);

        var result = Interval.Ceil(input, minutes);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Floor_ShouldFloorSubMinuteToStartOfInterval()
    {
        var input = new DateTime(2024, 1, 15, 9, 0, 30);

        var result = Interval.Floor(input, 30);

        Assert.Equal(new DateTime(2024, 1, 15, 9, 0, 0), result);
    }

    [Fact]
    public void Ceil_ShouldCeilSubMinuteToEndOfInterval()
    {
        var input = new DateTime(2024, 1, 15, 9, 0, 30);

        var result = Interval.Ceil(input, 30);

        Assert.Equal(new DateTime(2024, 1, 15, 9, 30, 0), result);
    }

    [Fact]
    public void Ceil_ShouldCeilAcrossMidnightToNextDay()
    {
        var input = new DateTime(2024, 1, 15, 23, 59, 0);

        var result = Interval.Ceil(input, 60);

        Assert.Equal(new DateTime(2024, 1, 16, 0, 0, 0), result);
    }
}

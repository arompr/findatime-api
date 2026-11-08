namespace FindAtime.UnitTests.Domain;

public class DayWindowTests
{
    [Theory]
    [InlineData(9, 21)]
    [InlineData(9, 17)]
    [InlineData(0, 24)]
    [InlineData(0, 1)]
    [InlineData(23, 24)]
    public void Validate_ShouldAcceptValidWindows(int startHour, int endHour)
    {
        DayWindow.Validate(startHour, endHour);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(17, 9)]
    [InlineData(-1, 9)]
    [InlineData(24, 24)]
    [InlineData(9, 25)]
    [InlineData(0, 0)]
    public void Validate_ShouldThrowForInvalidWindows(int startHour, int endHour)
    {
        Assert.Throws<InvalidDayWindowException>(() => DayWindow.Validate(startHour, endHour));
    }

    [Fact]
    public void Defaults_ShouldBeNineToTwentyOne()
    {
        Assert.Equal(9, DayWindow.DefaultStartHour);
        Assert.Equal(21, DayWindow.DefaultEndHour);
        Assert.True(DayWindow.IsValid(DayWindow.DefaultStartHour, DayWindow.DefaultEndHour));
    }
}

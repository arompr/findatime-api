namespace FindAtime.UnitTests.Domain;

public class AvailabilityFactoryTests
{
    private readonly AvailabilityFactory _factory = new();
    private readonly ParticipantId _participantId = ParticipantId.FromString("p-1");

    [Fact]
    public void Create_ShouldReturnRangeWithGivenBounds()
    {
        var start = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);

        var range = _factory.Create(_participantId, start, end);

        Assert.Equal(_participantId, range.ParticipantId);
        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
    }

    [Fact]
    public void Create_ShouldGenerateId()
    {
        var start = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);

        var range = _factory.Create(_participantId, start, end);

        Assert.False(string.IsNullOrEmpty(range.Id.Value));
    }

    [Fact]
    public void Create_ShouldThrowWhenEndEqualsStart()
    {
        var instant = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);

        Assert.Throws<InvalidAvailabilityRangeException>(
            () => _factory.Create(_participantId, instant, instant));
    }

    [Fact]
    public void Create_ShouldThrowWhenEndBeforeStart()
    {
        var start = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);

        Assert.Throws<InvalidAvailabilityRangeException>(
            () => _factory.Create(_participantId, start, end));
    }

    [Fact]
    public void Create_ShouldThrowWhenDurationExceeds24Hours()
    {
        var start = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 20, 9, 0, 1, TimeSpan.Zero);

        Assert.Throws<InvalidAvailabilityRangeException>(
            () => _factory.Create(_participantId, start, end));
    }

    [Fact]
    public void Create_ShouldAllowExactly24Hours()
    {
        var start = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

        var range = _factory.Create(_participantId, start, end);

        Assert.Equal(TimeSpan.FromHours(24), range.End - range.Start);
    }
}

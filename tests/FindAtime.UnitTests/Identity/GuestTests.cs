namespace FindAtime.UnitTests.Identity;

public class GuestTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static Guest NewGuest() =>
        new(GuestId.FromString(Guid.NewGuid().ToString()), GuestTokenHash.FromBytes([1, 2, 3]), CreatedAt);

    [Fact]
    public void MarkSeen_ShouldKeepLastSeenWithinTheResolution()
    {
        var guest = NewGuest();

        guest.MarkSeen(CreatedAt + Guest.LastSeenResolution - TimeSpan.FromSeconds(1));

        Assert.Equal(CreatedAt, guest.LastSeenAt);
    }

    [Fact]
    public void MarkSeen_ShouldUpdateLastSeenOnceTheResolutionHasElapsed()
    {
        var guest = NewGuest();
        var now = CreatedAt + Guest.LastSeenResolution;

        guest.MarkSeen(now);

        Assert.Equal(now, guest.LastSeenAt);
    }

    [Fact]
    public void MarkSeen_ShouldNotMoveLastSeenBackwards()
    {
        var guest = NewGuest();

        guest.MarkSeen(CreatedAt - TimeSpan.FromDays(1));

        Assert.Equal(CreatedAt, guest.LastSeenAt);
    }
}

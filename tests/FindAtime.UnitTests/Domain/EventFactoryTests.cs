namespace FindAtime.UnitTests.Domain;

public class EventFactoryTests
{
    private readonly ParticipantFactory _participantFactory = new();
    private readonly EventFactory _eventFactory;

    public EventFactoryTests()
    {
        _eventFactory = new EventFactory(_participantFactory);
    }

    [Fact]
    public void CreateEvent_ShouldReturnEventWithCorrectName()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Assert.Equal("Team Meeting", @event.Name);
    }

    [Fact]
    public void CreateEvent_ShouldSetPublicId()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Assert.Equal(publicId, @event.PublicId);
    }

    [Fact]
    public void CreateEvent_ShouldGenerateEventId()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Assert.False(string.IsNullOrEmpty(@event.Id.Value));
    }

    [Fact]
    public void CreateEvent_ShouldHaveOrganizerAsFirstParticipant()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Participant organizer = Assert.Single(@event.Participants);
        Assert.Equal("Alice", organizer.Name);
    }

    [Fact]
    public void CreateEvent_ShouldSetPasscodeHashAndSalt()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Assert.Equal([1, 2, 3], @event.PasscodeHash!.Hash);
        Assert.Equal([4, 5, 6], @event.PasscodeHash.Salt);
    }

    [Fact]
    public void CreateEvent_ShouldNotBePasscodeProtectedWhenNoHash()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, null);

        Assert.False(@event.IsPasscodeProtected);
        Assert.Null(@event.PasscodeHash);
    }

    [Fact]
    public void CreateEvent_ShouldDefaultParamsToInterval30()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, null);

        Assert.Equal(30, @event.Params.IntervalMinutes);
    }

    [Fact]
    public void CreateEvent_ShouldDefaultTimezoneToNull()
    {
        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", PublicId.FromString("abc123"), null);

        Assert.Null(@event.Params.Timezone);
    }

    [Fact]
    public void CreateEvent_ShouldAnchorEventAndOrganizerToGivenTimezone()
    {
        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", PublicId.FromString("abc123"), null, "America/New_York");

        Assert.Equal("America/New_York", @event.Params.Timezone);
        Assert.Equal("America/New_York", @event.FindParticipant(@event.OrganizerParticipantId)!.Timezone);
    }
}

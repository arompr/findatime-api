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

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, [1, 2, 3], [4, 5, 6]);

        Assert.Equal("Team Meeting", @event.Name);
    }

    [Fact]
    public void CreateEvent_ShouldSetPublicId()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, [1, 2, 3], [4, 5, 6]);

        Assert.Equal(publicId, @event.PublicId);
    }

    [Fact]
    public void CreateEvent_ShouldGenerateEventId()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, [1, 2, 3], [4, 5, 6]);

        Assert.False(string.IsNullOrEmpty(@event.Id.Value));
    }

    [Fact]
    public void CreateEvent_ShouldHaveOrganizerAsFirstParticipant()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, [1, 2, 3], [4, 5, 6]);

        Participant organizer = Assert.Single(@event.Participants);
        Assert.Equal("Alice", organizer.Name);
    }

    [Fact]
    public void CreateEvent_ShouldSetPasscodeHashAndSalt()
    {
        var publicId = PublicId.FromString("abc123");

        var @event = _eventFactory.CreateEvent("Team Meeting", "uuid-1", "Alice", publicId, [1, 2, 3], [4, 5, 6]);

        Assert.Equal([1, 2, 3], @event.PasscodeHash);
        Assert.Equal([4, 5, 6], @event.PasscodeSalt);
    }
}
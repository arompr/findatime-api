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
        var publicId = new PublicId("abc123");

        var @event = _eventFactory.createEvent("Team Meeting", "uuid-1", "Alice", publicId);

        Assert.Equal("Team Meeting", @event.Name);
    }

    [Fact]
    public void CreateEvent_ShouldSetPublicId()
    {
        var publicId = new PublicId("abc123");

        var @event = _eventFactory.createEvent("Team Meeting", "uuid-1", "Alice", publicId);

        Assert.Same(publicId, @event.PublicId);
    }

    [Fact]
    public void CreateEvent_ShouldGenerateEventId()
    {
        var publicId = new PublicId("abc123");

        var @event = _eventFactory.createEvent("Team Meeting", "uuid-1", "Alice", publicId);

        Assert.False(string.IsNullOrEmpty(@event.Id.Value));
    }

    [Fact]
    public void CreateEvent_ShouldHaveOrganizerAsFirstParticipant()
    {
        var publicId = new PublicId("abc123");

        var @event = _eventFactory.createEvent("Team Meeting", "uuid-1", "Alice", publicId);

        Participant organizer = Assert.Single(@event.Participants);
        Assert.Equal("Alice", organizer.Name);
    }
}
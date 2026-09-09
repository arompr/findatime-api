namespace FindAtime.UnitTests.Domain;

public class ParticipantFactoryTests
{
    private readonly ParticipantFactory _factory = new();

    [Fact]
    public void CreateParticipant_ShouldSetParticipantUuid()
    {
        var eventId = new EventId("event-1");

        var participant = _factory.createParticipant("test-uuid", "Alice", eventId);

        Assert.Equal("test-uuid", participant.ParticipantUuid.Value);
    }

    [Fact]
    public void CreateParticipant_ShouldSetName()
    {
        var eventId = new EventId("event-1");

        var participant = _factory.createParticipant("test-uuid", "Alice", eventId);

        Assert.Equal("Alice", participant.Name);
    }

    [Fact]
    public void CreateParticipant_ShouldSetEventId()
    {
        var eventId = new EventId("event-1");

        var participant = _factory.createParticipant("test-uuid", "Alice", eventId);

        Assert.Same(eventId, participant.EventId);
    }

    [Fact]
    public void CreateParticipant_ShouldGenerateParticipantId()
    {
        var eventId = new EventId("event-1");

        var participant = _factory.createParticipant("test-uuid", "Alice", eventId);

        Assert.False(string.IsNullOrEmpty(participant.ParticipantId.Value));
    }
}
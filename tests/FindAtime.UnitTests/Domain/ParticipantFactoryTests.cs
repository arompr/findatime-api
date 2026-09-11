namespace FindAtime.UnitTests.Domain;

public class ParticipantFactoryTests
{
    private readonly ParticipantFactory _factory = new();

    [Fact]
    public void CreateParticipant_ShouldSetParticipantUuid()
    {
        var eventId = EventId.FromString("event-1");

        var participant = _factory.CreateParticipant("test-uuid", "Alice", eventId);

        Assert.Equal("test-uuid", participant.ParticipantUuid.Value);
    }

    [Fact]
    public void CreateParticipant_ShouldSetName()
    {
        var eventId = EventId.FromString("event-1");

        var participant = _factory.CreateParticipant("test-uuid", "Alice", eventId);

        Assert.Equal("Alice", participant.Name);
    }

    [Fact]
    public void CreateParticipant_ShouldSetEventId()
    {
        var eventId = EventId.FromString("event-1");

        var participant = _factory.CreateParticipant("test-uuid", "Alice", eventId);

        Assert.Equal(eventId, participant.EventId);
    }

    [Fact]
    public void CreateParticipant_ShouldGenerateParticipantId()
    {
        var eventId = EventId.FromString("event-1");

        var participant = _factory.CreateParticipant("test-uuid", "Alice", eventId);

        Assert.False(string.IsNullOrEmpty(participant.ParticipantId.Value));
    }
}
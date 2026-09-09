namespace FindAtime.UnitTests.Domain;

public class EventTests
{
    [Fact]
    public void Create_ShouldSetIdAndNameAndPublicId()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var participantId = ParticipantId.FromString("p-1");
        var participant = new Participant(participantId, ParticipantUuid.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant);

        Assert.Equal(id, @event.Id);
        Assert.Equal("Team Meeting", @event.Name);
        Assert.Equal(publicId, @event.PublicId);
    }

    [Fact]
    public void Create_ShouldSetOrganizerParticipantId()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var participantId = ParticipantId.FromString("p-1");
        var participant = new Participant(participantId, ParticipantUuid.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant);

        Assert.Equal(participantId, @event.OrganizerParticipantId);
    }

    [Fact]
    public void Create_ShouldAddOrganizerAsFirstParticipant()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var participantId = ParticipantId.FromString("p-1");
        var participant = new Participant(participantId, ParticipantUuid.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant);

        Participant organizer = Assert.Single(@event.Participants);
        Assert.Same(participant, organizer);
    }

    [Fact]
    public void Create_ShouldSetParticipantEventId()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var participantId = ParticipantId.FromString("p-1");
        var participant = new Participant(participantId, ParticipantUuid.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        Event.Create(id, publicId, "Team Meeting", participant);

        Assert.Equal(id, participant.EventId);
    }

    [Fact]
    public void AddParticipant_ShouldAddParticipantToList()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var organizerId = ParticipantId.FromString("p-1");
        var organizer = new Participant(organizerId, ParticipantUuid.FromString("uuid-1"), "Alice", EventId.FromString("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer);

        var newParticipant = new Participant(ParticipantId.FromString("p-2"), ParticipantUuid.FromString("uuid-2"), "Bob", EventId.FromString("temp"));
        @event.AddParticipant(newParticipant);

        Assert.Equal(2, @event.Participants.Count);
        Assert.Contains(newParticipant, @event.Participants);
    }

    [Fact]
    public void AddParticipant_ShouldSetParticipantEventId()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var organizerId = ParticipantId.FromString("p-1");
        var organizer = new Participant(organizerId, ParticipantUuid.FromString("uuid-1"), "Alice", EventId.FromString("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer);

        var newParticipant = new Participant(ParticipantId.FromString("p-2"), ParticipantUuid.FromString("uuid-2"), "Bob", EventId.FromString("temp"));
        @event.AddParticipant(newParticipant);

        Assert.Equal(id, newParticipant.EventId);
    }
}
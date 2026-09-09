namespace FindAtime.UnitTests.Domain;

public class EventTests
{
    [Fact]
    public void Create_ShouldSetIdAndNameAndPublicId()
    {
        var id = new EventId("event-1");
        var publicId = new PublicId("abc123");
        var participantId = new ParticipantId("p-1");
        var participant = new Participant(participantId, new ParticipantUuid("uuid-1"), "Alice", new EventId("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant);

        Assert.Same(id, @event.Id);
        Assert.Equal("Team Meeting", @event.Name);
        Assert.Same(publicId, @event.PublicId);
    }

    [Fact]
    public void Create_ShouldSetOrganizerParticipantId()
    {
        var id = new EventId("event-1");
        var publicId = new PublicId("abc123");
        var participantId = new ParticipantId("p-1");
        var participant = new Participant(participantId, new ParticipantUuid("uuid-1"), "Alice", new EventId("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant);

        Assert.Same(participantId, @event.OrganizerParticipantId);
    }

    [Fact]
    public void Create_ShouldAddOrganizerAsFirstParticipant()
    {
        var id = new EventId("event-1");
        var publicId = new PublicId("abc123");
        var participantId = new ParticipantId("p-1");
        var participant = new Participant(participantId, new ParticipantUuid("uuid-1"), "Alice", new EventId("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant);

        Participant organizer = Assert.Single(@event.Participants);
        Assert.Same(participant, organizer);
    }

    [Fact]
    public void Create_ShouldSetParticipantEventId()
    {
        var id = new EventId("event-1");
        var publicId = new PublicId("abc123");
        var participantId = new ParticipantId("p-1");
        var participant = new Participant(participantId, new ParticipantUuid("uuid-1"), "Alice", new EventId("temp"));

        Event.Create(id, publicId, "Team Meeting", participant);

        Assert.Same(id, participant.EventId);
    }

    [Fact]
    public void AddParticipant_ShouldAddParticipantToList()
    {
        var id = new EventId("event-1");
        var publicId = new PublicId("abc123");
        var organizerId = new ParticipantId("p-1");
        var organizer = new Participant(organizerId, new ParticipantUuid("uuid-1"), "Alice", new EventId("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer);

        var newParticipant = new Participant(new ParticipantId("p-2"), new ParticipantUuid("uuid-2"), "Bob", new EventId("temp"));
        @event.AddParticipant(newParticipant);

        Assert.Equal(2, @event.Participants.Count);
        Assert.Contains(newParticipant, @event.Participants);
    }

    [Fact]
    public void AddParticipant_ShouldSetParticipantEventId()
    {
        var id = new EventId("event-1");
        var publicId = new PublicId("abc123");
        var organizerId = new ParticipantId("p-1");
        var organizer = new Participant(organizerId, new ParticipantUuid("uuid-1"), "Alice", new EventId("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer);

        var newParticipant = new Participant(new ParticipantId("p-2"), new ParticipantUuid("uuid-2"), "Bob", new EventId("temp"));
        @event.AddParticipant(newParticipant);

        Assert.Same(id, newParticipant.EventId);
    }
}
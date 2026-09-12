namespace FindAtime.UnitTests.Domain;

public class EventTests
{
    [Fact]
    public void Create_ShouldSetIdAndNameAndPublicId()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var participantId = ParticipantId.FromString("p-1");
        var participant = new Participant(participantId, GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

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
        var participant = new Participant(participantId, GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Assert.Equal(participantId, @event.OrganizerParticipantId);
    }

    [Fact]
    public void Create_ShouldAddOrganizerAsFirstParticipant()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var participantId = ParticipantId.FromString("p-1");
        var participant = new Participant(participantId, GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        var @event = Event.Create(id, publicId, "Team Meeting", participant, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Participant organizer = Assert.Single(@event.Participants);
        Assert.Same(participant, organizer);
    }

    [Fact]
    public void Create_ShouldSetParticipantEventId()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var participantId = ParticipantId.FromString("p-1");
        var participant = new Participant(participantId, GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));

        Event.Create(id, publicId, "Team Meeting", participant, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        Assert.Equal(id, participant.EventId);
    }

    [Fact]
    public void AddParticipant_ShouldAddParticipantToList()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var organizerId = ParticipantId.FromString("p-1");
        var organizer = new Participant(organizerId, GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        var newParticipant = new Participant(ParticipantId.FromString("p-2"), GuestId.FromString("uuid-2"), "Bob", EventId.FromString("temp"));
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
        var organizer = new Participant(organizerId, GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        var newParticipant = new Participant(ParticipantId.FromString("p-2"), GuestId.FromString("uuid-2"), "Bob", EventId.FromString("temp"));
        @event.AddParticipant(newParticipant);

        Assert.Equal(id, newParticipant.EventId);
    }

    [Fact]
    public void RemoveParticipant_ShouldRemoveMatchingParticipantAndReturnTrue()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var organizer = new Participant(ParticipantId.FromString("p-1"), GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        var friend = new Participant(ParticipantId.FromString("p-2"), GuestId.FromString("uuid-2"), "Bob", EventId.FromString("temp"));
        @event.AddParticipant(friend);

        bool removed = @event.RemoveParticipant(GuestId.FromString("uuid-2"));

        Assert.True(removed);
        Assert.DoesNotContain(friend, @event.Participants);
    }

    [Fact]
    public void RemoveParticipant_ShouldReturnFalseWhenNoMatch()
    {
        var id = EventId.FromString("event-1");
        var publicId = PublicId.FromString("abc123");
        var organizer = new Participant(ParticipantId.FromString("p-1"), GuestId.FromString("uuid-1"), "Alice", EventId.FromString("temp"));
        var @event = Event.Create(id, publicId, "Team Meeting", organizer, PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        bool removed = @event.RemoveParticipant(GuestId.FromString("uuid-999"));

        Assert.False(removed);
        Assert.Single(@event.Participants);
    }
}
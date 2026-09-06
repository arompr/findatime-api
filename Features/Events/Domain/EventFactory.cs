class EventFactory
{
    private ParticipantFactory _participantFactory;

    public EventFactory(ParticipantFactory participantFactory)
    {
        this._participantFactory = participantFactory;
    }

    public Event createEvent(string name, string participantUuid)
    {
        Participant creatorParticipant = this._participantFactory.createParticipant(participantUuid);
        Event domainEvent = new Event(
            new EventId(Guid.NewGuid().ToString()),
            name,
            creatorParticipant.ParticipantId
        );
        domainEvent.AddParticipant(creatorParticipant);
        return domainEvent;
    }
}
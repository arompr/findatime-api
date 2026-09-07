class EventFactory
{
    private ParticipantFactory _participantFactory;

    public EventFactory(ParticipantFactory participantFactory)
    {
        this._participantFactory = participantFactory;
    }

    public Event createEvent(string name, string participantUuid)
    {
        Participant creator = this._participantFactory.createParticipant(participantUuid);
        EventId eventId = new EventId(Guid.NewGuid().ToString());
        return Event.Create(eventId, name, creator);
    }
}

class EventFactory
{
    private ParticipantFactory _participantFactory;

    public EventFactory(ParticipantFactory participantFactory)
    {
        this._participantFactory = participantFactory;
    }

    public Event createEvent(string name, string participantUuid, string participantName, PublicId publicId)
    {
        EventId eventId = new EventId(Guid.NewGuid().ToString());
        Participant organizer = this._participantFactory.createParticipant(participantUuid, participantName, eventId);
        return Event.Create(eventId, publicId, name, organizer);
    }
}

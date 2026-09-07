class EventFactory
{
    private ParticipantFactory _participantFactory;

    public EventFactory(ParticipantFactory participantFactory)
    {
        this._participantFactory = participantFactory;
    }

    public Event createEvent(string name, string participantUuid, string participantName, PublicId publicId)
    {
        Participant organizer = this._participantFactory.createParticipant(participantUuid, participantName);
        EventId eventId = new EventId(Guid.NewGuid().ToString());
        return Event.Create(eventId, publicId, name, organizer);
    }
}

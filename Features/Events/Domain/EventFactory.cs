public class EventFactory
{
    private ParticipantFactory _participantFactory;

    public EventFactory(ParticipantFactory participantFactory)
    {
        this._participantFactory = participantFactory;
    }

    public Event CreateEvent(string name, string participantUuid, string participantName, PublicId publicId)
    {
        EventId eventId = EventId.FromString(Guid.NewGuid().ToString());
        Participant organizer = this._participantFactory.CreateParticipant(participantUuid, participantName, eventId);
        return Event.Create(eventId, publicId, name, organizer);
    }
}

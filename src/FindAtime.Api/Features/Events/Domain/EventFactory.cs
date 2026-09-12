public class EventFactory
{
    private ParticipantFactory _participantFactory;

    public EventFactory(ParticipantFactory participantFactory)
    {
        this._participantFactory = participantFactory;
    }

    public Event CreateEvent(string name, string guestId, string organizerName, PublicId publicId, PasscodeHash passcodeHash)
    {
        EventId eventId = EventId.FromString(Guid.NewGuid().ToString());
        Participant organizer = this._participantFactory.CreateParticipant(guestId, organizerName, eventId);
        return Event.Create(eventId, publicId, name, organizer, passcodeHash);
    }
}

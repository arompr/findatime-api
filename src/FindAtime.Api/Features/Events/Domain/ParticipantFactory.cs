public class ParticipantFactory
{
    public Participant CreateParticipant(string guestId, string name, EventId eventId)
    {
        return new Participant(
            ParticipantId.FromString(Guid.NewGuid().ToString()),
            GuestId.FromString(guestId),
            name,
            eventId
        );
    }
}
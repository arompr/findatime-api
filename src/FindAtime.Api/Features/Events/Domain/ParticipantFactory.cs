public class ParticipantFactory
{
    public Participant CreateParticipant(string guestId, string name, EventId eventId, string? timezone = null)
    {
        Participant participant = new Participant(
            ParticipantId.FromString(Guid.NewGuid().ToString()),
            GuestId.FromString(guestId),
            name,
            eventId
        );
        participant.SetTimezone(timezone);
        return participant;
    }
}
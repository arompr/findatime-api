public class ParticipantFactory
{
    public Participant CreateParticipant(string participantUuid, string name, EventId eventId)
    {
        return new Participant(
            ParticipantId.FromString(Guid.NewGuid().ToString()),
            ParticipantUuid.FromString(participantUuid),
            name,
            eventId
        );
    }
}
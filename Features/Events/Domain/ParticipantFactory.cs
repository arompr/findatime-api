class ParticipantFactory
{
    public Participant createParticipant(string participantUuid, string name, EventId eventId)
    {
        return new Participant(
            new ParticipantId(Guid.NewGuid().ToString()),
            new ParticipantUuid(participantUuid),
            name,
            eventId
        );
    }
}
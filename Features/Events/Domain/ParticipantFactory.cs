class ParticipantFactory
{
    public Participant createParticipant(string participantUuid)
    {
        return new Participant(
            new ParticipantId(Guid.NewGuid().ToString()),
            new ParticipantUuid(participantUuid)
        );
    }
}
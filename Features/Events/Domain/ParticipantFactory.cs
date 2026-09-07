class ParticipantFactory
{
    public Participant createParticipant(string participantUuid, string name)
    {
        return new Participant(
            new ParticipantId(Guid.NewGuid().ToString()),
            new ParticipantUuid(participantUuid),
            name
        );
    }
}
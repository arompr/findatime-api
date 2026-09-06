class Participant
{
    public ParticipantId ParticipantId { get; set; }
    public ParticipantUuid ParticipantUuid { get; set; }

    public Participant(ParticipantId participantId, ParticipantUuid participantUuid)
    {
        this.ParticipantId = participantId;
        this.ParticipantUuid = participantUuid;
    }
}
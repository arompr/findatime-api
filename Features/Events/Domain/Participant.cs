class Participant
{
    public ParticipantId ParticipantId { get; set; }
    public ParticipantUuid ParticipantUuid { get; set; }
    public string Name { get; set; }

    public Participant(ParticipantId participantId, ParticipantUuid participantUuid, string name)
    {
        this.ParticipantId = participantId;
        this.ParticipantUuid = participantUuid;
        this.Name = name;
    }
}
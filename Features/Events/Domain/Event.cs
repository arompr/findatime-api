class Event
{
    public EventId EventId { get; set; }
    public string Name { get; set; }
    public ParticipantId CreatorParticipantId { get; set; }
    public List<Participant> Participants { get; } = new List<Participant>();

    public Event(EventId eventId, string Name, ParticipantId creatorParticipantId)
    {
        this.EventId = eventId;
        this.Name = Name;
        this.CreatorParticipantId = creatorParticipantId;
    }

    public void AddParticipant(Participant participant)
    {
        this.Participants.Add(participant);
    }
}
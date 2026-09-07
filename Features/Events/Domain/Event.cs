class Event
{
    private readonly List<Participant> _participants;

    public EventId Id { get; }
    public string Name { get; }
    public ParticipantId CreatorParticipantId { get; }
    public IReadOnlyCollection<Participant> Participants => _participants;

    public Event(EventId id, string name, ParticipantId creatorParticipantId, List<Participant> participants)
    {
        this.Id = id;
        this.Name = name;
        this.CreatorParticipantId = creatorParticipantId;
        this._participants = participants;
    }

    public static Event Create(EventId id, string name, Participant creator)
    {
        return new Event(id, name, creator.ParticipantId, [creator]);
    }

    public void AddParticipant(Participant participant)
    {
        this._participants.Add(participant);
    }
}

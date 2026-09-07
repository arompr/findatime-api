class Event
{
    private readonly List<Participant> _participants;

    public EventId Id { get; }
    public PublicId PublicId { get; }
    public string Name { get; }
    public ParticipantId OrganizerParticipantId { get; }
    public IReadOnlyCollection<Participant> Participants => _participants;

    public Event(EventId id, PublicId publicId, string name, ParticipantId organizerParticipantId, List<Participant> participants)
    {
        this.Id = id;
        this.PublicId = publicId;
        this.Name = name;
        this.OrganizerParticipantId = organizerParticipantId;
        this._participants = participants;
    }

    public static Event Create(EventId id, PublicId publicId, string name, Participant organizer)
    {
        return new Event(id, publicId, name, organizer.ParticipantId, [organizer]);
    }

    public void AddParticipant(Participant participant)
    {
        this._participants.Add(participant);
    }
}

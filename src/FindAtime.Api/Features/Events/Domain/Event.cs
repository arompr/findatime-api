using System.ComponentModel.DataAnnotations.Schema;

[Table("events")]
[Index(nameof(PublicId), IsUnique = true)]
public class Event
{
    private List<Participant> _participants;

    [Column("id")]
    public EventId Id { get; private set; }

    [Column("public_id")]
    public PublicId PublicId { get; private set; }

    [Column("name")]
    public string Name { get; private set; }

    [Column("organizer_participant_id")]
    public ParticipantId OrganizerParticipantId { get; private set; }

    public PasscodeHash PasscodeHash { get; private set; }

    public IReadOnlyCollection<Participant> Participants => _participants;

    private Event()
    {
        this._participants = new List<Participant>();
        this.PasscodeHash = PasscodeHash.FromBytes([], []);
    }

    public Event(EventId id, PublicId publicId, string name, ParticipantId organizerParticipantId, PasscodeHash passcodeHash, List<Participant> participants)
    {
        this.Id = id;
        this.PublicId = publicId;
        this.Name = name;
        this.OrganizerParticipantId = organizerParticipantId;
        this.PasscodeHash = passcodeHash;
        this._participants = participants;
    }

    public static Event Create(EventId id, PublicId publicId, string name, Participant organizer, PasscodeHash passcodeHash)
    {
        organizer.EventId = id;
        return new Event(id, publicId, name, organizer.ParticipantId, passcodeHash, [organizer]);
    }

    public void AddParticipant(Participant participant)
    {
        participant.EventId = this.Id;
        this._participants.Add(participant);
    }

    public bool RemoveParticipant(GuestId guestId)
    {
        int removed = this._participants.RemoveAll(p => p.GuestId == guestId);
        return removed > 0;
    }
}
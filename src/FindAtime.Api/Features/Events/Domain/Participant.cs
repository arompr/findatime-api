using System.ComponentModel.DataAnnotations.Schema;

[Table("participants")]
public class Participant
{
    [Column("id")]
    public ParticipantId ParticipantId { get; set; }

    [Column("guest_id")]
    public GuestId GuestId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("event_id")]
    public EventId EventId { get; set; }

    public Participant(ParticipantId participantId, GuestId guestId, string name, EventId eventId)
    {
        this.ParticipantId = participantId;
        this.GuestId = guestId;
        this.Name = name;
        this.EventId = eventId;
    }
}
using System.ComponentModel.DataAnnotations.Schema;

[Table("participants")]
public class Participant
{
    [Column("id")]
    public ParticipantId ParticipantId { get; set; }

    [Column("participant_uuid")]
    public ParticipantUuid ParticipantUuid { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("event_id")]
    public EventId EventId { get; set; }

    public Participant(ParticipantId participantId, ParticipantUuid participantUuid, string name, EventId eventId)
    {
        this.ParticipantId = participantId;
        this.ParticipantUuid = participantUuid;
        this.Name = name;
        this.EventId = eventId;
    }
}
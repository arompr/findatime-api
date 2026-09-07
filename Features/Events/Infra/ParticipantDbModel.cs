using System.ComponentModel.DataAnnotations.Schema;

[Table("participants")]
class ParticipantDbModel
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("participant_uuid")]
    public Guid ParticipantUuid { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("event_id")]
    public Guid EventId { get; set; }
}
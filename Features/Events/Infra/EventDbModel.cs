using System.ComponentModel.DataAnnotations.Schema;

[Table("events")]
class EventDbModel
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("creator_participant_id")]
    public Guid CreatorParticipantId { get; set; }
}
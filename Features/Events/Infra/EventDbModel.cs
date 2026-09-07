using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("events")]
[Index(nameof(PublicId), IsUnique = true)]
class EventDbModel
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("organizer_participant_id")]
    public Guid OrganizerParticipantId { get; set; }

    [Column("public_id")]
    public required string PublicId { get; set; }
}
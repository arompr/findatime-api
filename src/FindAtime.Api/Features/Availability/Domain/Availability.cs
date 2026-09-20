using System.ComponentModel.DataAnnotations.Schema;

[Table("availabilities")]
public class Availability
{
    [Column("id")]
    public AvailabilityId Id { get; private set; }

    [Column("participant_id")]
    public ParticipantId ParticipantId { get; private set; }

    [Column("start_utc")]
    public DateTimeOffset Start { get; private set; }

    [Column("end_utc")]
    public DateTimeOffset End { get; private set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; private set; }

    private Availability() { }

    public Availability(
        AvailabilityId id,
        ParticipantId participantId,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset createdAt
    )
    {
        this.Id = id;
        this.ParticipantId = participantId;
        this.Start = start;
        this.End = end;
        this.CreatedAt = createdAt;
    }
}

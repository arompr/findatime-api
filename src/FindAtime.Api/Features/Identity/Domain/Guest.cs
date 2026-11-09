using System.ComponentModel.DataAnnotations.Schema;

[Table("guests")]
public class Guest
{
    [Column("id")]
    public GuestId Id { get; private set; }

    [Column("token_hash")]
    public byte[] TokenHash { get; private set; } = null!;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; private set; }

    [Column("last_seen_at")]
    public DateTimeOffset LastSeenAt { get; private set; }

    private Guest() { }

    public Guest(GuestId id, byte[] tokenHash, DateTimeOffset createdAt)
    {
        this.Id = id;
        this.TokenHash = tokenHash;
        this.CreatedAt = createdAt;
        this.LastSeenAt = createdAt;
    }
}

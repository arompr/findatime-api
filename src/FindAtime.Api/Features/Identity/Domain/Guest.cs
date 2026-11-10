using System.ComponentModel.DataAnnotations.Schema;

[Table("guests")]
public class Guest
{
    public static readonly TimeSpan LastSeenResolution = TimeSpan.FromHours(1);

    [Column("id")]
    public GuestId Id { get; private set; }

    [Column("token_hash")]
    public GuestTokenHash TokenHash { get; private set; } = null!;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; private set; }

    [Column("last_seen_at")]
    public DateTimeOffset LastSeenAt { get; private set; }

    private Guest() { }

    public Guest(GuestId id, GuestTokenHash tokenHash, DateTimeOffset createdAt)
    {
        this.Id = id;
        this.TokenHash = tokenHash;
        this.CreatedAt = createdAt;
        this.LastSeenAt = createdAt;
    }

    public static bool IsSeenUpdateDue(DateTimeOffset lastSeenAt, DateTimeOffset now) =>
        now - lastSeenAt >= LastSeenResolution;

    public void MarkSeen(DateTimeOffset now)
    {
        if (IsSeenUpdateDue(this.LastSeenAt, now))
            this.LastSeenAt = now;
    }
}

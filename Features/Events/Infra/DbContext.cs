using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class DbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public DbSet<Event> Events { get; set; }
    public DbSet<Participant> Participants { get; set; }

    public DbContext(DbContextOptions<DbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Id).HasConversion(
                id => Guid.Parse(id.Value),
                value => new EventId(value.ToString()));

            entity.Property(e => e.PublicId).HasConversion(
                publicId => publicId.Value,
                value => new PublicId(value));

            entity.Property(e => e.OrganizerParticipantId).HasConversion(
                id => Guid.Parse(id.Value),
                value => new ParticipantId(value.ToString()));
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.Property(p => p.ParticipantId).HasConversion(
                id => Guid.Parse(id.Value),
                value => new ParticipantId(value.ToString()));

            entity.Property(p => p.ParticipantUuid).HasConversion(
                participantUuid => Guid.Parse(participantUuid.Value),
                value => new ParticipantUuid(value.ToString()));

            entity.Property(p => p.EventId).HasConversion(
                id => Guid.Parse(id.Value),
                value => new EventId(value.ToString()));
        });
    }
}
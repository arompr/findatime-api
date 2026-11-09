using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class DbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public DbSet<Event> Events { get; set; }
    public DbSet<Participant> Participants { get; set; }
    public DbSet<Availability> Availabilities { get; set; }
    public DbSet<Guest> Guests { get; set; }

    public DbContext(DbContextOptions<DbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Id).HasConversion(
                id => Guid.Parse(id.Value),
                value => EventId.FromString(value.ToString()));

            entity.Property(e => e.PublicId).HasConversion(
                publicId => publicId.Value,
                value => PublicId.FromString(value));

            entity.Property(e => e.OrganizerParticipantId).HasConversion(
                id => Guid.Parse(id.Value),
                value => ParticipantId.FromString(value.ToString()));

            entity.ComplexProperty(e => e.PasscodeHash, complex =>
            {
                complex.Property(p => p.Hash).HasColumnName("passcode_hash");
                complex.Property(p => p.Salt).HasColumnName("passcode_salt");
            });

            entity.ComplexProperty(e => e.Params, c =>
            {
                c.Property(p => p.Timezone).HasColumnName("timezone");
                c.Property(p => p.IntervalMinutes)
                    .HasColumnName("interval_minutes")
                    .HasDefaultValue(Interval.DefaultMinutes)
                    .IsRequired();
                c.Property(p => p.DayStartHour)
                    .HasColumnName("day_start_hour")
                    .HasDefaultValue(DayWindow.DefaultStartHour)
                    .HasSentinel(-1)
                    .IsRequired();
                c.Property(p => p.DayEndHour)
                    .HasColumnName("day_end_hour")
                    .HasDefaultValue(DayWindow.DefaultEndHour)
                    .IsRequired();
            });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_events_day_window",
                    "\"day_start_hour\" >= 0 AND \"day_end_hour\" <= 24 AND \"day_start_hour\" < \"day_end_hour\"");
            });
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.Property(p => p.ParticipantId).HasConversion(
                id => Guid.Parse(id.Value),
                value => ParticipantId.FromString(value.ToString()));

            entity.Property(p => p.GuestId).HasConversion(
                guestId => Guid.Parse(guestId.Value),
                value => GuestId.FromString(value.ToString()));

            entity.Property(p => p.EventId).HasConversion(
                id => Guid.Parse(id.Value),
                value => EventId.FromString(value.ToString()));

            entity.HasIndex(p => new { p.EventId, p.GuestId }).IsUnique();
        });

        modelBuilder.Entity<Guest>(entity =>
        {
            entity.Property(g => g.Id).HasConversion(
                id => Guid.Parse(id.Value),
                value => GuestId.FromString(value.ToString()));

            entity.HasKey(g => g.Id);
            entity.HasIndex(g => g.TokenHash).IsUnique();
        });

        modelBuilder.Entity<Availability>(entity =>
        {
            entity.Property(a => a.Id).HasConversion(
                id => Guid.Parse(id.Value),
                value => AvailabilityId.FromString(value.ToString()));

            entity.Property(a => a.ParticipantId).HasConversion(
                id => Guid.Parse(id.Value),
                value => ParticipantId.FromString(value.ToString()));

            entity.HasOne<Participant>()
                .WithMany()
                .HasForeignKey(a => a.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_availabilities_end_after_start",
                    "\"end_utc\" > \"start_utc\"");
                t.HasCheckConstraint(
                    "ck_availabilities_max_duration",
                    "\"end_utc\" - \"start_utc\" <= interval '24 hours'");
            });
        });
    }
}
class DbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public DbSet<EventDbModel> Events { get; set; }
    public DbSet<ParticipantDbModel> Participants { get; set; }

    public DbContext(DbContextOptions<DbContext> options)
        : base(options) { }
}


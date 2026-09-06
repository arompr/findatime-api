class ReadDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options)
        : base(options) { }
}

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql(dataSource)
            .Options;

        await using var context = new DbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

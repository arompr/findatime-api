using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public abstract class IntegrationTest : IAsyncLifetime
{
    protected PostgresFixture Postgres { get; }

    protected IServiceScope Scope { get; private set; } = default!;

    private NpgsqlConnection _connection = default!;
    private NpgsqlTransaction _transaction = default!;
    private ServiceProvider _provider = default!;

    protected IntegrationTest(PostgresFixture postgres)
    {
        Postgres = postgres;
    }

    public virtual async Task InitializeAsync()
    {
        _connection = new NpgsqlConnection(Postgres.ConnectionString);
        await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();

        var services = new ServiceCollection();
        services.AddScoped<IReadConnectionProvider>(_ => new TransactionalReadConnectionProvider(_connection));
        services.AddDbContext<DbContext>(o => o.UseNpgsql(_connection));
        services.AddScoped<EventRepository>();
        services.AddScoped<ReadEventService>();
        services.AddSingleton<EventFactory>();
        services.AddSingleton<ParticipantFactory>();
        services.AddSingleton<PublicIdGenerator>();
        services.AddSingleton<PasscodeGenerator>();
        services.AddSingleton<PasscodeHasher>();
        services.AddScoped<CreateEvent>();
        services.AddScoped<GetEvent>();
        services.AddScoped<GetEventByPublicId>();
        services.AddScoped<JoinEvent>();
        services.AddScoped<LeaveEvent>();
        services.AddScoped<SearchEvents>();

        _provider = services.BuildServiceProvider();
        Scope = _provider.CreateScope();

        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        await db.Database.UseTransactionAsync(_transaction);
    }

    public virtual async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();

        await ((IAsyncDisposable)Scope).DisposeAsync();
        await _provider.DisposeAsync();
    }
}

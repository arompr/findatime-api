using Npgsql;

public sealed class TransactionalReadConnectionProvider : IReadConnectionProvider
{
    private readonly NpgsqlConnection _connection;

    public TransactionalReadConnectionProvider(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public Task<NpgsqlConnection> OpenAsync() => Task.FromResult(_connection);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

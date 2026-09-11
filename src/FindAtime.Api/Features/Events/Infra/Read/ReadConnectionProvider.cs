using Npgsql;

public sealed class ReadConnectionProvider : IReadConnectionProvider
{
    private readonly NpgsqlDataSource _dataSource;
    private NpgsqlConnection? _connection;

    public ReadConnectionProvider(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        return _connection ??= await _dataSource.OpenConnectionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null) return;
        await _connection.DisposeAsync();
        _connection = null;
    }
}

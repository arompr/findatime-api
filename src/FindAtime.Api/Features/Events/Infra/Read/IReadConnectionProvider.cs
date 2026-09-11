using Npgsql;

public interface IReadConnectionProvider : IAsyncDisposable
{
    Task<NpgsqlConnection> OpenAsync();
}

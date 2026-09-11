using Dapper;

public class ReadEventService
{
    private static readonly string GetEventSql = Sql.Load("get_event.sql");

    private IReadConnectionProvider _connectionProvider;

    public ReadEventService(IReadConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<EventReadDto?> GetEvent(Guid eventId)
    {
        var connection = await _connectionProvider.OpenAsync();

        await using var reader = await connection.ExecuteReaderAsync(GetEventSql, new { id = eventId });

        if (!await reader.ReadAsync())
            return null;

        return new EventReadDto(
            reader.GetGuid(reader.GetOrdinal("id")).ToString(),
            reader.GetString(reader.GetOrdinal("name"))
        );
    }
}

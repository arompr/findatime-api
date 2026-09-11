using Dapper;
using Npgsql;

public class ReadEventService
{
    private static readonly string GetEventSql = Sql.Load("get_event.sql");

    private NpgsqlDataSource _dataSource;

    public ReadEventService(NpgsqlDataSource dataSource)
    {
        this._dataSource = dataSource;
    }

    public async Task<EventReadDto?> GetEvent(Guid eventId)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        await using var reader = await connection.ExecuteReaderAsync(GetEventSql, new { id = eventId });

        if (!await reader.ReadAsync())
            return null;

        return new EventReadDto(
            reader.GetGuid(reader.GetOrdinal("id")).ToString(),
            reader.GetString(reader.GetOrdinal("name"))
        );
    }
}

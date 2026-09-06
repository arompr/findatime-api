using System.Data;
using Dapper;

class ReadEventService
{
    private static readonly string GetEventSql = Sql.Load("get_event.sql");

    private ReadDbContext _readDbContext;

    public ReadEventService(ReadDbContext readDbContext)
    {
        this._readDbContext = readDbContext;
    }

    public async Task<EventReadDto?> GetEvent(Guid eventId)
    {
        var connection = _readDbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var reader = await connection.ExecuteReaderAsync(
            GetEventSql,
            new { id = eventId }
        );

        if (!await reader.ReadAsync())
            return null;

        return new EventReadDto(
            reader.GetGuid(reader.GetOrdinal("id")).ToString(),
            reader.GetString(reader.GetOrdinal("name"))
        );
    }
}

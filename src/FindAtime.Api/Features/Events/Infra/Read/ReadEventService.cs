using Dapper;

public class ReadEventService
{
    private static readonly string GetEventSql = Sql.Load("get_event.sql");
    private static readonly string GetEventByPublicIdSql = Sql.Load("get_event_by_public_id.sql");
    private static readonly string SearchEventsSql = Sql.Load("search_events.sql");

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

    public async Task<GetEventByPublicIdResponse?> GetEventByPublicId(string publicId)
    {
        var connection = await _connectionProvider.OpenAsync();

        await using var reader = await connection.ExecuteReaderAsync(GetEventByPublicIdSql, new { publicId });

        if (!await reader.ReadAsync())
            return null;

        return new GetEventByPublicIdResponse(
            reader.GetGuid(reader.GetOrdinal("id")).ToString(),
            reader.GetString(reader.GetOrdinal("name"))
        );
    }

    public async Task<IReadOnlyList<EventSummaryDto>> SearchEvents(Guid guestId)
    {
        var connection = await _connectionProvider.OpenAsync();

        await using var reader = await connection.ExecuteReaderAsync(SearchEventsSql, new { guestId });

        var results = new List<EventSummaryDto>();
        while (await reader.ReadAsync())
        {
            results.Add(new EventSummaryDto(
                reader.GetString(reader.GetOrdinal("public_id")),
                reader.GetString(reader.GetOrdinal("name"))
            ));
        }

        return results;
    }
}

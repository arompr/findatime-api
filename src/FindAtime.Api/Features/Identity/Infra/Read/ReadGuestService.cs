using Dapper;

public class ReadGuestService
{
    private static readonly string GetGuestByTokenHashSql = Sql.Load("get_guest_by_token_hash.sql");

    private IReadConnectionProvider _connectionProvider;

    public ReadGuestService(IReadConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<GuestDto?> FindByTokenHash(GuestTokenHash tokenHash)
    {
        var connection = await _connectionProvider.OpenAsync();

        await using var reader = await connection.ExecuteReaderAsync(
            GetGuestByTokenHashSql, new { tokenHash = tokenHash.Value });

        if (!await reader.ReadAsync())
            return null;

        return new GuestDto(
            reader.GetGuid(reader.GetOrdinal("guest_id")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("last_seen_at"))
        );
    }
}

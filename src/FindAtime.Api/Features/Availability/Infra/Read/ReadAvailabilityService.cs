using Dapper;

public class ReadAvailabilityService
{
    private static readonly string GetParticipantByEventAndGuestSql = Sql.Load("get_participant_by_event_and_guest.sql");
    private static readonly string GetEventAvailabilitiesSql = Sql.Load("get_event_availabilities.sql");

    private IReadConnectionProvider _connectionProvider;

    public ReadAvailabilityService(IReadConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<ParticipantDto?> GetParticipantByEventAndGuest(string publicId, Guid guestId)
    {
        var connection = await _connectionProvider.OpenAsync();

        await using var reader = await connection.ExecuteReaderAsync(
            GetParticipantByEventAndGuestSql, new { publicId, guestId });

        if (!await reader.ReadAsync())
            return null;

        return new ParticipantDto(
            reader.GetGuid(reader.GetOrdinal("participant_id")).ToString(),
            reader.GetString(reader.GetOrdinal("name"))
        );
    }

    public async Task<EventAvailabilityDto?> GetAllForEvent(string publicId)
    {
        var connection = await _connectionProvider.OpenAsync();

        await using var reader = await connection.ExecuteReaderAsync(
            GetEventAvailabilitiesSql, new { publicId });

        if (!await reader.ReadAsync())
            return null;

        var timezoneOrdinal = reader.GetOrdinal("timezone");
        string? eventTimezone = reader.IsDBNull(timezoneOrdinal)
            ? null
            : reader.GetString(timezoneOrdinal);

        await reader.NextResultAsync();

        var participants = new List<ParticipantAvailabilityDto>();
        var rangesByParticipant = new Dictionary<string, List<AvailabilityRangeDto>>();

        while (await reader.ReadAsync())
        {
            var participantId = reader.GetGuid(reader.GetOrdinal("participant_id")).ToString();

            if (!rangesByParticipant.TryGetValue(participantId, out var ranges))
            {
                ranges = [];
                rangesByParticipant[participantId] = ranges;
                participants.Add(new ParticipantAvailabilityDto(
                    participantId,
                    reader.GetString(reader.GetOrdinal("name")),
                    ranges
                ));
            }

            var startOrdinal = reader.GetOrdinal("start_utc");
            if (reader.IsDBNull(startOrdinal))
                continue;

            ranges.Add(new AvailabilityRangeDto(
                reader.GetFieldValue<DateTimeOffset>(startOrdinal),
                reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("end_utc"))
            ));
        }

        return new EventAvailabilityDto(eventTimezone, participants);
    }
}

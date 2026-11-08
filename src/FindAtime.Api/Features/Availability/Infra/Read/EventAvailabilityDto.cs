public record EventAvailabilityDto(string? EventTimezone, int IntervalMinutes, IReadOnlyList<ParticipantAvailabilityDto> Participants);

public record ParticipantAvailabilityDto(string ParticipantId, string Name, IReadOnlyList<AvailabilityRangeDto> Ranges);

public record AvailabilityRangeDto(DateTimeOffset Start, DateTimeOffset End);

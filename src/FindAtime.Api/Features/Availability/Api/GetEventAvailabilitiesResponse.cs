public record GetEventAvailabilitiesResponse(string? EventTimezone, IReadOnlyList<ParticipantAvailabilityResponse> Participants);

public record ParticipantAvailabilityResponse(string ParticipantId, string Name, IReadOnlyList<AvailabilityRangeResponse> Ranges);

public record AvailabilityRangeResponse(DateTimeOffset Start, DateTimeOffset End);

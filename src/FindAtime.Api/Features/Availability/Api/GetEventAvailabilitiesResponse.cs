public record GetEventAvailabilitiesResponse(string Timezone, IReadOnlyList<ParticipantAvailabilityResponse> Participants);

public record ParticipantAvailabilityResponse(string ParticipantId, string Name, IReadOnlyList<AvailabilityRangeResponse> Ranges);

public record AvailabilityRangeResponse(DateTime Start, DateTime End);

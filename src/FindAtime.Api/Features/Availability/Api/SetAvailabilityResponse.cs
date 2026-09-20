public record SetAvailabilityResponse(string ParticipantId, string Name, IReadOnlyList<AvailabilityRangeResponse> Ranges);

public record GetParticipantAvailabilityResponse(string ParticipantId, string Name, IReadOnlyList<AvailabilityRangeResponse> Ranges);

public record GetParticipantAvailabilityResponse(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResponse> Ranges);

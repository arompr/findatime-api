public record SetAvailabilityResponse(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResponse> Ranges);

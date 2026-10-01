public record SetAvailabilityResult(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResult> Ranges);

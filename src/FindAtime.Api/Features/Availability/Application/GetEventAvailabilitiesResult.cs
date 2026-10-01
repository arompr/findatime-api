public record GetEventAvailabilitiesResult(string Timezone, IReadOnlyList<ParticipantAvailabilityResult> Participants);

public record ParticipantAvailabilityResult(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResult> Ranges);

public record AvailabilityRangeResult(DateTime Start, DateTime End);

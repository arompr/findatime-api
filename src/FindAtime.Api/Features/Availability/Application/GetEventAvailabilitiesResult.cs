public record GetEventAvailabilitiesResult(string? EventTimezone, IReadOnlyList<ParticipantAvailabilityResult> Participants);

public record ParticipantAvailabilityResult(string ParticipantId, string Name, IReadOnlyList<AvailabilityRangeResult> Ranges);

public record AvailabilityRangeResult(DateTimeOffset Start, DateTimeOffset End);

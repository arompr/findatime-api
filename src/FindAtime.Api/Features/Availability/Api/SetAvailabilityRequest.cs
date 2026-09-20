public record SetAvailabilityRequest(IReadOnlyList<AvailabilityRangeRequest> Ranges);

public record AvailabilityRangeRequest(DateTimeOffset Start, DateTimeOffset End);

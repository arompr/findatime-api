public record SetAvailabilityRequest(IReadOnlyList<AvailabilityRangeRequest> Ranges);

public record AvailabilityRangeRequest(string Start, string End);

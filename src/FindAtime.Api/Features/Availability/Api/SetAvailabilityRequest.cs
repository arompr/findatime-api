public record SetAvailabilityRequest(string Timezone, IReadOnlyList<AvailabilityRangeRequest> Ranges);

public record AvailabilityRangeRequest(string Start, string End);

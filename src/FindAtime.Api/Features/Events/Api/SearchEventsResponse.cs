public record SearchEventsResponse(IReadOnlyList<EventSummaryResponse> Events);

public record EventSummaryResponse(string PublicId, string Name);

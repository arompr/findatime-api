public class SearchEvents
{
    private ReadEventService _readEventService;

    public SearchEvents(ReadEventService readEventService)
    {
        _readEventService = readEventService;
    }

    public async Task<IReadOnlyList<EventSummaryDto>> Execute(Guid guestId)
    {
        return await _readEventService.SearchEvents(guestId);
    }
}

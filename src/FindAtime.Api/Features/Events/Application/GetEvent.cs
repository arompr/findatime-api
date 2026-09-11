public class GetEvent
{
    private ReadEventService _readEventService;

    public GetEvent(ReadEventService readEventService)
    {
        this._readEventService = readEventService;
    }

    public async Task<EventReadDto> Execute(Guid eventId)
    {
        EventReadDto? eventReadDto = await this._readEventService.GetEvent(eventId);
        if (eventReadDto is null)
            throw new EventNotFoundException(eventId);

        return eventReadDto;
    }
}

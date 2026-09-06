class GetEvent
{
    private ReadEventService _readEventService;

    public GetEvent(ReadEventService readEventService)
    {
        this._readEventService = readEventService;
    }

    public Task<EventReadDto?> Execute(Guid eventId)
    {
        return this._readEventService.GetEvent(eventId);
    }
}
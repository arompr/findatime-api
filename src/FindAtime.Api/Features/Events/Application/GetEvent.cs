public class GetEvent
{
    private ReadEventService _readEventService;

    public GetEvent(ReadEventService readEventService)
    {
        this._readEventService = readEventService;
    }

    public async Task<GetEventResponse> Execute(string publicId)
    {
        GetEventResponse? response = await this._readEventService.GetEventByPublicId(publicId);
        if (response is null)
            throw new EventNotFoundException(publicId);

        return response;
    }
}

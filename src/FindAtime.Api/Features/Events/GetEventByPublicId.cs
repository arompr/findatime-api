public class GetEventByPublicId
{
    private ReadEventService _readEventService;

    public GetEventByPublicId(ReadEventService readEventService)
    {
        this._readEventService = readEventService;
    }

    public Task<GetEventByPublicIdResponse?> Execute(string publicId)
    {
        return this._readEventService.GetEventByPublicId(publicId);
    }
}

public class GetEventByPublicId
{
    private ReadEventService _readEventService;

    public GetEventByPublicId(ReadEventService readEventService)
    {
        this._readEventService = readEventService;
    }

    public async Task<GetEventByPublicIdResponse> Execute(string publicId)
    {
        GetEventByPublicIdResponse? response = await this._readEventService.GetEventByPublicId(publicId);
        if (response is null)
            throw new EventNotFoundException(publicId);

        return response;
    }
}

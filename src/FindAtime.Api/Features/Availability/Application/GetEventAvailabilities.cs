public class GetEventAvailabilities
{
    private ReadAvailabilityService _readAvailabilityService;

    public GetEventAvailabilities(ReadAvailabilityService readAvailabilityService)
    {
        _readAvailabilityService = readAvailabilityService;
    }

    public async Task<GetEventAvailabilitiesResult> Execute(string publicId)
    {
        EventAvailabilityDto? dto = await _readAvailabilityService.GetAllForEvent(publicId);
        if (dto is null)
            throw new EventNotFoundException(publicId);

        return new GetEventAvailabilitiesResult(
            dto.EventTimezone,
            dto.Participants
                .Select(p => new ParticipantAvailabilityResult(
                    p.ParticipantId,
                    p.Name,
                    p.Ranges
                        .Select(r => new AvailabilityRangeResult(r.Start, r.End))
                        .ToList()
                ))
                .ToList()
        );
    }
}

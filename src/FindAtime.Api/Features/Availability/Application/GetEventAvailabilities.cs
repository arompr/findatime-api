public class GetEventAvailabilities
{
    private ReadAvailabilityService _readAvailabilityService;
    private AvailabilityProjection _availabilityProjection;

    public GetEventAvailabilities(
        ReadAvailabilityService readAvailabilityService,
        AvailabilityProjection availabilityProjection)
    {
        _readAvailabilityService = readAvailabilityService;
        _availabilityProjection = availabilityProjection;
    }

    public async Task<GetEventAvailabilitiesResult> Execute(string publicId)
    {
        EventAvailabilityDto? dto = await _readAvailabilityService.GetAllForEvent(publicId);
        if (dto is null)
            throw new EventNotFoundException(publicId);

        string anchorTimezone = dto.EventTimezone ?? "UTC";

        return new GetEventAvailabilitiesResult(
            dto.Participants
                .Select(p => new ParticipantAvailabilityResult(
                    p.ParticipantId,
                    p.Name,
                    _availabilityProjection
                        .Project(
                            p.Ranges.Select(r => (Start: r.Start, End: r.End)),
                            dto.IntervalMinutes,
                            anchorTimezone)
                        .Select(r => new AvailabilityRangeResult(r.Start.ToUniversalTime(), r.End.ToUniversalTime()))
                        .ToList()
                ))
                .ToList()
        );
    }
}

public class GetEventAvailabilities
{
    private ReadAvailabilityService _readAvailabilityService;
    private TimezoneConverter _timezoneConverter;
    private AvailabilityProjection _availabilityProjection;

    public GetEventAvailabilities(
        ReadAvailabilityService readAvailabilityService,
        TimezoneConverter timezoneConverter,
        AvailabilityProjection availabilityProjection)
    {
        _readAvailabilityService = readAvailabilityService;
        _timezoneConverter = timezoneConverter;
        _availabilityProjection = availabilityProjection;
    }

    public async Task<GetEventAvailabilitiesResult> Execute(string publicId, string? viewerTimezone)
    {
        EventAvailabilityDto? dto = await _readAvailabilityService.GetAllForEvent(publicId);
        if (dto is null)
            throw new EventNotFoundException(publicId);

        string frame = viewerTimezone ?? dto.EventTimezone ?? "UTC";
        if (dto.EventTimezone is null)
            frame = "UTC";

        string anchorTimezone = dto.EventTimezone ?? "UTC";

        return new GetEventAvailabilitiesResult(
            frame,
            dto.Participants
                .Select(p => new ParticipantAvailabilityResult(
                    p.ParticipantId,
                    p.Name,
                    frame,
                    _availabilityProjection
                        .Project(
                            p.Ranges.Select(r => (Start: r.Start, End: r.End)),
                            dto.IntervalMinutes,
                            anchorTimezone)
                        .Select(r => new AvailabilityRangeResult(
                            _timezoneConverter.ToWallClock(r.Start, frame),
                            _timezoneConverter.ToWallClock(r.End, frame)))
                        .ToList()
                ))
                .ToList()
        );
    }
}

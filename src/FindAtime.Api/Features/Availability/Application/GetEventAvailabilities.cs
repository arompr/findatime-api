public class GetEventAvailabilities
{
    private ReadAvailabilityService _readAvailabilityService;
    private TimezoneConverter _timezoneConverter;

    public GetEventAvailabilities(ReadAvailabilityService readAvailabilityService, TimezoneConverter timezoneConverter)
    {
        _readAvailabilityService = readAvailabilityService;
        _timezoneConverter = timezoneConverter;
    }

    public async Task<GetEventAvailabilitiesResult> Execute(string publicId, string? viewerTimezone)
    {
        EventAvailabilityDto? dto = await _readAvailabilityService.GetAllForEvent(publicId);
        if (dto is null)
            throw new EventNotFoundException(publicId);

        string frame = viewerTimezone ?? dto.EventTimezone ?? "UTC";
        if (dto.EventTimezone is null)
            frame = "UTC";

        return new GetEventAvailabilitiesResult(
            frame,
            dto.Participants
                .Select(p => new ParticipantAvailabilityResult(
                    p.ParticipantId,
                    p.Name,
                    frame,
                    p.Ranges
                        .Select(r => new AvailabilityRangeResult(
                            _timezoneConverter.ToWallClock(r.Start, frame),
                            _timezoneConverter.ToWallClock(r.End, frame)))
                        .ToList()
                ))
                .ToList()
        );
    }
}

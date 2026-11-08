public class GetParticipantAvailability
{
    private ReadEventService _readEventService;
    private ReadAvailabilityService _readAvailabilityService;
    private TimezoneConverter _timezoneConverter;
    private AvailabilityProjection _availabilityProjection;

    public GetParticipantAvailability(
        ReadEventService readEventService,
        ReadAvailabilityService readAvailabilityService,
        TimezoneConverter timezoneConverter,
        AvailabilityProjection availabilityProjection)
    {
        _readEventService = readEventService;
        _readAvailabilityService = readAvailabilityService;
        _timezoneConverter = timezoneConverter;
        _availabilityProjection = availabilityProjection;
    }

    public async Task<ParticipantAvailabilityResult> Execute(string publicId, Guid participantId, string? viewerTimezone)
    {
        EventDto? eventDto = await _readEventService.GetEventByPublicId(publicId);
        if (eventDto is null)
            throw new EventNotFoundException(publicId);

        ParticipantAvailabilityDto? dto = await _readAvailabilityService.GetForParticipant(publicId, participantId);
        if (dto is null)
            throw new ParticipantNotFoundException(publicId, participantId.ToString());

        string frame = viewerTimezone ?? eventDto.Timezone ?? "UTC";
        if (eventDto.Timezone is null)
            frame = "UTC";

        string anchorTimezone = eventDto.Timezone ?? "UTC";

        return new ParticipantAvailabilityResult(
            dto.ParticipantId,
            dto.Name,
            frame,
            _availabilityProjection
                .Project(
                    dto.Ranges.Select(r => (Start: r.Start, End: r.End)),
                    eventDto.IntervalMinutes,
                    anchorTimezone)
                .Select(r => new AvailabilityRangeResult(
                    _timezoneConverter.ToWallClock(r.Start, frame),
                    _timezoneConverter.ToWallClock(r.End, frame)))
                .ToList()
        );
    }
}

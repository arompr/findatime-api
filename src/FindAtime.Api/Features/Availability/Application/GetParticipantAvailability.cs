public class GetParticipantAvailability
{
    private ReadEventService _readEventService;
    private ReadAvailabilityService _readAvailabilityService;
    private TimezoneConverter _timezoneConverter;

    public GetParticipantAvailability(ReadEventService readEventService, ReadAvailabilityService readAvailabilityService, TimezoneConverter timezoneConverter)
    {
        _readEventService = readEventService;
        _readAvailabilityService = readAvailabilityService;
        _timezoneConverter = timezoneConverter;
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

        return new ParticipantAvailabilityResult(
            dto.ParticipantId,
            dto.Name,
            frame,
            dto.Ranges
                .Select(r => new AvailabilityRangeResult(
                    _timezoneConverter.ToWallClock(r.Start, frame),
                    _timezoneConverter.ToWallClock(r.End, frame)))
                .ToList()
        );
    }
}

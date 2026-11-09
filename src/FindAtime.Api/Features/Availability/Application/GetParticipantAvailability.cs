public class GetParticipantAvailability
{
    private ReadEventService _readEventService;
    private ReadAvailabilityService _readAvailabilityService;
    private AvailabilityProjection _availabilityProjection;

    public GetParticipantAvailability(
        ReadEventService readEventService,
        ReadAvailabilityService readAvailabilityService,
        AvailabilityProjection availabilityProjection)
    {
        _readEventService = readEventService;
        _readAvailabilityService = readAvailabilityService;
        _availabilityProjection = availabilityProjection;
    }

    public async Task<ParticipantAvailabilityResult> Execute(string publicId, Guid participantId, Guid guestId)
    {
        EventDto? eventDto = await _readEventService.GetEventByPublicId(publicId);
        if (eventDto is null)
            throw new EventNotFoundException(publicId);

        if (await _readAvailabilityService.GetParticipantByEventAndGuest(publicId, guestId) is null)
            throw new ForbiddenException($"Guest id '{guestId}' is not a participant of event '{publicId}'.");

        ParticipantAvailabilityDto? dto = await _readAvailabilityService.GetForParticipant(publicId, participantId);
        if (dto is null)
            throw new ParticipantNotFoundException(publicId, participantId.ToString());

        string anchorTimezone = eventDto.Timezone ?? "UTC";

        return new ParticipantAvailabilityResult(
            dto.ParticipantId,
            dto.Name,
            _availabilityProjection
                .Project(
                    dto.Ranges.Select(r => (Start: r.Start, End: r.End)),
                    eventDto.IntervalMinutes,
                    anchorTimezone)
                .Select(r => new AvailabilityRangeResult(r.Start.ToUniversalTime(), r.End.ToUniversalTime()))
                .ToList()
        );
    }
}

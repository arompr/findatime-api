public class GetParticipantAvailability
{
    private ReadEventService _readEventService;
    private ReadAvailabilityService _readAvailabilityService;

    public GetParticipantAvailability(ReadEventService readEventService, ReadAvailabilityService readAvailabilityService)
    {
        _readEventService = readEventService;
        _readAvailabilityService = readAvailabilityService;
    }

    public async Task<ParticipantAvailabilityResult> Execute(string publicId, Guid participantId)
    {
        EventDto? eventDto = await _readEventService.GetEventByPublicId(publicId);
        if (eventDto is null)
            throw new EventNotFoundException(publicId);

        ParticipantAvailabilityDto? dto = await _readAvailabilityService.GetForParticipant(publicId, participantId);
        if (dto is null)
            throw new ParticipantNotFoundException(publicId, participantId.ToString());

        return new ParticipantAvailabilityResult(
            dto.ParticipantId,
            dto.Name,
            dto.Ranges
                .Select(r => new AvailabilityRangeResult(r.Start, r.End))
                .ToList()
        );
    }
}

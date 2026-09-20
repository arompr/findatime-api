public class GetMyParticipant
{
    private ReadEventService _readEventService;
    private ReadAvailabilityService _readAvailabilityService;

    public GetMyParticipant(ReadEventService readEventService, ReadAvailabilityService readAvailabilityService)
    {
        _readEventService = readEventService;
        _readAvailabilityService = readAvailabilityService;
    }

    public async Task<GetMyParticipantResult> Execute(string publicId, Guid guestId)
    {
        EventDto? eventDto = await _readEventService.GetEventByPublicId(publicId);
        if (eventDto is null)
            throw new EventNotFoundException(publicId);

        ParticipantDto? participant = await _readAvailabilityService.GetParticipantByEventAndGuest(publicId, guestId);
        if (participant is null)
            throw new ParticipantNotFoundException(publicId, guestId);

        return new GetMyParticipantResult(participant.ParticipantId, participant.Name);
    }
}

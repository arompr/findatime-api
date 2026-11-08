public class SetAvailability
{
    private EventRepository _eventRepository;
    private AvailabilityFactory _availabilityFactory;
    private AvailabilityRepository _availabilityRepository;
    private AvailabilityProjection _availabilityProjection;

    public SetAvailability(
        EventRepository eventRepository,
        AvailabilityFactory availabilityFactory,
        AvailabilityRepository availabilityRepository,
        AvailabilityProjection availabilityProjection
    )
    {
        _eventRepository = eventRepository;
        _availabilityFactory = availabilityFactory;
        _availabilityRepository = availabilityRepository;
        _availabilityProjection = availabilityProjection;
    }

    public async Task<SetAvailabilityResult> Execute(
        string publicId,
        Guid participantId,
        Guid guestId,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> ranges
    )
    {
        Event? domainEvent = await _eventRepository.GetByPublicId(publicId);
        if (domainEvent is null)
            throw new EventNotFoundException(publicId);

        int intervalMinutes = domainEvent.Params.IntervalMinutes;
        string anchorTimezone = domainEvent.Params.Timezone ?? "UTC";

        ParticipantId pid = ParticipantId.FromString(participantId.ToString());

        Participant? participant = domainEvent.FindParticipant(pid);
        if (participant is null)
            throw new ParticipantNotFoundException(publicId, participantId.ToString());

        GuestId requestGuestId = GuestId.FromString(guestId.ToString());
        if (participant.GuestId != requestGuestId)
            throw new ForbiddenException($"Guest id '{guestId}' does not match participant '{participantId}'.");

        List<Availability> availabilities = ranges
            .Select(r =>
            {
                if (r.End <= r.Start)
                    throw new InvalidAvailabilityRangeException("Availability range must end after it starts.");

                var snapped = _availabilityProjection.SnapOutward(r, intervalMinutes, anchorTimezone);
                return _availabilityFactory.Create(
                    pid,
                    snapped.Start.ToUniversalTime(),
                    snapped.End.ToUniversalTime());
            })
            .ToList();

        await _availabilityRepository.DeleteForParticipant(pid);
        await _availabilityRepository.Save(availabilities);

        return new SetAvailabilityResult(
            participant.ParticipantId.Value,
            participant.Name,
            availabilities
                .Select(a => new AvailabilityRangeResult(a.Start, a.End))
                .ToList()
        );
    }
}

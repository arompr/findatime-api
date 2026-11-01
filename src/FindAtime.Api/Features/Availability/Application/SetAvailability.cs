public class SetAvailability
{
    private EventRepository _eventRepository;
    private AvailabilityFactory _availabilityFactory;
    private AvailabilityRepository _availabilityRepository;
    private TimezoneConverter _timezoneConverter;

    public SetAvailability(
        EventRepository eventRepository,
        AvailabilityFactory availabilityFactory,
        AvailabilityRepository availabilityRepository,
        TimezoneConverter timezoneConverter
    )
    {
        _eventRepository = eventRepository;
        _availabilityFactory = availabilityFactory;
        _availabilityRepository = availabilityRepository;
        _timezoneConverter = timezoneConverter;
    }

    public async Task<SetAvailabilityResult> Execute(
        string publicId,
        Guid participantId,
        Guid guestId,
        string timezone,
        IReadOnlyList<AvailabilityRangeResult> ranges
    )
    {
        Event? domainEvent = await _eventRepository.GetByPublicId(publicId);
        if (domainEvent is null)
            throw new EventNotFoundException(publicId);

        int intervalMinutes = domainEvent.Params.IntervalMinutes;

        ParticipantId pid = ParticipantId.FromString(participantId.ToString());

        Participant? participant = domainEvent.FindParticipant(pid);
        if (participant is null)
            throw new ParticipantNotFoundException(publicId, participantId.ToString());

        GuestId requestGuestId = GuestId.FromString(guestId.ToString());
        if (participant.GuestId != requestGuestId)
            throw new ForbiddenException($"Guest id '{guestId}' does not match participant '{participantId}'.");

        _timezoneConverter.Resolve(timezone);
        participant.SetTimezone(timezone);

        List<Availability> availabilities = ranges
            .Select(r => _availabilityFactory.Create(
                pid,
                _timezoneConverter.ToUtc(Interval.SnapStart(r.Start, intervalMinutes), timezone),
                _timezoneConverter.ToUtc(Interval.SnapEnd(r.End, intervalMinutes), timezone)))
            .ToList();

        await _availabilityRepository.DeleteForParticipant(pid);
        await _availabilityRepository.Save(availabilities);

        return new SetAvailabilityResult(
            participant.ParticipantId.Value,
            participant.Name,
            timezone,
            availabilities
                .Select(a => new AvailabilityRangeResult(
                    _timezoneConverter.ToWallClock(a.Start, timezone),
                    _timezoneConverter.ToWallClock(a.End, timezone)))
                .ToList()
        );
    }
}

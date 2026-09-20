public class SetAvailability
{
    private EventRepository _eventRepository;
    private AvailabilityFactory _availabilityFactory;
    private AvailabilityRepository _availabilityRepository;

    public SetAvailability(
        EventRepository eventRepository,
        AvailabilityFactory availabilityFactory,
        AvailabilityRepository availabilityRepository
    )
    {
        _eventRepository = eventRepository;
        _availabilityFactory = availabilityFactory;
        _availabilityRepository = availabilityRepository;
    }

    public async Task<SetAvailabilityResult> Execute(
        string publicId,
        Guid participantId,
        Guid guestId,
        IReadOnlyList<AvailabilityRangeResult> ranges
    )
    {
        Event? domainEvent = await _eventRepository.GetByPublicId(publicId);
        if (domainEvent is null)
            throw new EventNotFoundException(publicId);

        ParticipantId pid = ParticipantId.FromString(participantId.ToString());

        Participant? participant = domainEvent.FindParticipant(pid);
        if (participant is null)
            throw new ParticipantNotFoundException(publicId, participantId.ToString());

        GuestId requestGuestId = GuestId.FromString(guestId.ToString());
        if (participant.GuestId != requestGuestId)
            throw new ForbiddenException($"Guest id '{guestId}' does not match participant '{participantId}'.");

        List<Availability> availabilities = ranges
            .Select(r => _availabilityFactory.Create(pid, r.Start, r.End))
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

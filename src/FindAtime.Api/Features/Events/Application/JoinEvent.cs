public class JoinEvent
{
    private EventRepository _eventRepository;
    private ParticipantFactory _participantFactory;
    private PasscodeHasher _passcodeHasher;

    public JoinEvent(EventRepository eventRepository, ParticipantFactory participantFactory, PasscodeHasher passcodeHasher)
    {
        this._eventRepository = eventRepository;
        this._participantFactory = participantFactory;
        this._passcodeHasher = passcodeHasher;
    }

    public async Task<JoinEventResponse> Execute(string publicId, string? passcode, string guestId, string participantName)
    {
        Event? domainEvent = await this._eventRepository.GetByPublicId(publicId);
        if (domainEvent is null)
            throw new EventNotFoundException(publicId);

        if (domainEvent.IsPasscodeProtected
            && (string.IsNullOrEmpty(passcode) || !this._passcodeHasher.Verify(passcode, domainEvent.PasscodeHash!)))
            throw new InvalidPasscodeException(publicId);

        var requestGuestId = GuestId.FromString(guestId);
        Participant? existing = domainEvent.Participants
            .SingleOrDefault(p => p.GuestId == requestGuestId);

        if (existing is not null)
            throw new ParticipantAlreadyJoinedException(publicId);

        Participant participant = this._participantFactory.CreateParticipant(guestId, participantName, domainEvent.Id);
        domainEvent.AddParticipant(participant);
        await this._eventRepository.Save(domainEvent);

        return new JoinEventResponse(participant.ParticipantId.Value, participant.Name);
    }
}

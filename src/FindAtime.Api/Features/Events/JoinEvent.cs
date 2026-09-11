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

    public async Task<JoinEventResult> Execute(Guid eventId, string passcode, string guestId, string participantName)
    {
        Event? domainEvent = await this._eventRepository.GetById(eventId);
        if (domainEvent is null)
            return new JoinEventResult(JoinEventStatus.EventNotFound, null);

        if (!this._passcodeHasher.Verify(passcode, domainEvent.PasscodeHash, domainEvent.PasscodeSalt))
            return new JoinEventResult(JoinEventStatus.InvalidPasscode, null);

        var requestGuestId = GuestId.FromString(guestId);
        Participant? existing = domainEvent.Participants
            .SingleOrDefault(p => p.GuestId == requestGuestId);

        if (existing is not null)
        {
            if (existing.Name == participantName)
                return new JoinEventResult(JoinEventStatus.AlreadyJoined, new JoinEventResponse(existing.ParticipantId.Value, existing.Name));

            return new JoinEventResult(JoinEventStatus.NameMismatch, null);
        }

        Participant participant = this._participantFactory.CreateParticipant(guestId, participantName, domainEvent.Id);
        domainEvent.AddParticipant(participant);
        await this._eventRepository.Save(domainEvent);

        return new JoinEventResult(JoinEventStatus.Joined, new JoinEventResponse(participant.ParticipantId.Value, participant.Name));
    }
}

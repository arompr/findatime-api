public class LeaveEvent
{
    private EventRepository _eventRepository;

    public LeaveEvent(EventRepository eventRepository)
    {
        this._eventRepository = eventRepository;
    }

    public async Task Execute(Guid eventId, string guestId)
    {
        Event? domainEvent = await this._eventRepository.GetById(eventId);
        if (domainEvent is null)
            throw new EventNotFoundException(eventId);

        var requestGuestId = GuestId.FromString(guestId);
        Participant? participant = domainEvent.Participants
            .SingleOrDefault(p => p.GuestId == requestGuestId);

        if (participant is null)
            return;

        if (participant.ParticipantId == domainEvent.OrganizerParticipantId)
            throw new OrganizerCannotLeaveException(eventId);

        domainEvent.RemoveParticipant(requestGuestId);
        await this._eventRepository.Save(domainEvent);
    }
}

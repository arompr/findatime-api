public class LeaveEvent
{
    private EventRepository _eventRepository;

    public LeaveEvent(EventRepository eventRepository)
    {
        this._eventRepository = eventRepository;
    }

    public async Task Execute(string publicId, string guestId)
    {
        Event? domainEvent = await this._eventRepository.GetByPublicId(publicId);
        if (domainEvent is null)
            throw new EventNotFoundException(publicId);

        var requestGuestId = GuestId.FromString(guestId);
        Participant? participant = domainEvent.Participants
            .SingleOrDefault(p => p.GuestId == requestGuestId);

        if (participant is null)
            return;

        if (participant.ParticipantId == domainEvent.OrganizerParticipantId)
            throw new OrganizerCannotLeaveException(publicId);

        domainEvent.RemoveParticipant(requestGuestId);
        await this._eventRepository.Save(domainEvent);
    }
}

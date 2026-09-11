public class LeaveEvent
{
    private EventRepository _eventRepository;

    public LeaveEvent(EventRepository eventRepository)
    {
        this._eventRepository = eventRepository;
    }

    public async Task<LeaveEventResult> Execute(Guid eventId, string guestId)
    {
        Event? domainEvent = await this._eventRepository.GetById(eventId);
        if (domainEvent is null)
            return new LeaveEventResult(LeaveEventStatus.NotFound);

        var requestGuestId = GuestId.FromString(guestId);
        Participant? participant = domainEvent.Participants
            .SingleOrDefault(p => p.GuestId == requestGuestId);

        if (participant is null)
            return new LeaveEventResult(LeaveEventStatus.Left);

        if (participant.ParticipantId == domainEvent.OrganizerParticipantId)
            return new LeaveEventResult(LeaveEventStatus.OrganizerCannotLeave);

        domainEvent.RemoveParticipant(requestGuestId);
        await this._eventRepository.Save(domainEvent);

        return new LeaveEventResult(LeaveEventStatus.Left);
    }
}

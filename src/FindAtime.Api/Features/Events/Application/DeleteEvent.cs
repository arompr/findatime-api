public class DeleteEvent
{
    private EventRepository _eventRepository;

    public DeleteEvent(EventRepository eventRepository)
    {
        this._eventRepository = eventRepository;
    }

    public async Task Execute(string publicId, Guid guestId)
    {
        Event? domainEvent = await this._eventRepository.GetByPublicId(publicId);
        if (domainEvent is null)
            throw new EventNotFoundException(publicId);

        Participant? organizer = domainEvent.FindParticipant(domainEvent.OrganizerParticipantId);
        GuestId requestGuestId = GuestId.FromString(guestId.ToString());
        if (organizer is null || organizer.GuestId != requestGuestId)
            throw new ForbiddenException($"Guest id '{guestId}' is not the organizer of event '{publicId}'.");

        await this._eventRepository.Delete(domainEvent);
    }
}

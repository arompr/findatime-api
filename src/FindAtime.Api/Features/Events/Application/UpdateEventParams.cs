public class UpdateEventParams
{
    private EventRepository _eventRepository;
    private TimezoneConverter _timezoneConverter;

    public UpdateEventParams(EventRepository eventRepository, TimezoneConverter timezoneConverter)
    {
        this._eventRepository = eventRepository;
        this._timezoneConverter = timezoneConverter;
    }

    public async Task<UpdateEventParamsResponse> Execute(string publicId, Guid guestId, string? timezone)
    {
        Event? domainEvent = await this._eventRepository.GetByPublicId(publicId);
        if (domainEvent is null)
            throw new EventNotFoundException(publicId);

        Participant? organizer = domainEvent.FindParticipant(domainEvent.OrganizerParticipantId);
        GuestId requestGuestId = GuestId.FromString(guestId.ToString());
        if (organizer is null || organizer.GuestId != requestGuestId)
            throw new ForbiddenException($"Guest id '{guestId}' is not the organizer of event '{publicId}'.");

        if (timezone is not null)
            this._timezoneConverter.Resolve(timezone);

        domainEvent.SetParams(new EventParams(timezone));
        await this._eventRepository.Save(domainEvent);

        return new UpdateEventParamsResponse(timezone);
    }
}

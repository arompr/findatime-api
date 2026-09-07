class CreateEvent
{
    private EventFactory _eventFactory;
    private EventRepository _eventRepository;

    public CreateEvent(EventFactory eventFactory, EventRepository eventRepository)
    {
        this._eventFactory = eventFactory;
        this._eventRepository = eventRepository;
    }

    public async Task<string> Execute(string eventName, string participantUuid)
    {
        Event domainEvent = this._eventFactory.createEvent(eventName, participantUuid);
        await this._eventRepository.Save(domainEvent);
        return domainEvent.Id.Value;
    }
}


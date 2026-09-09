public class CreateEvent
{
    private EventFactory _eventFactory;
    private EventRepository _eventRepository;
    private PublicIdGenerator _publicIdGenerator;

    public CreateEvent(EventFactory eventFactory, EventRepository eventRepository, PublicIdGenerator publicIdGenerator)
    {
        this._eventFactory = eventFactory;
        this._eventRepository = eventRepository;
        this._publicIdGenerator = publicIdGenerator;
    }

    public async Task<string> Execute(string eventName, string participantUuid, string participantName)
    {
        PublicId publicId = this._publicIdGenerator.Generate();
        Event domainEvent = this._eventFactory.CreateEvent(eventName, participantUuid, participantName, publicId);
        await this._eventRepository.Save(domainEvent);
        return domainEvent.Id.Value;
    }
}


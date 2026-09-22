public class CreateEvent
{
    private EventFactory _eventFactory;
    private EventRepository _eventRepository;
    private PublicIdGenerator _publicIdGenerator;
    private PasscodeFactory _passcodeFactory;

    public CreateEvent(
        EventFactory eventFactory,
        EventRepository eventRepository,
        PublicIdGenerator publicIdGenerator,
        PasscodeFactory passcodeFactory
    )
    {
        this._eventFactory = eventFactory;
        this._eventRepository = eventRepository;
        this._publicIdGenerator = publicIdGenerator;
        this._passcodeFactory = passcodeFactory;
    }

    public async Task<CreateEventResponse> Execute(
        string eventName,
        string guestId,
        string organizerName,
        bool isPasscodeProtected
    )
    {
        PublicId publicId = this._publicIdGenerator.Generate();
        PasscodeCreated? passcode = null;
        if (isPasscodeProtected)
        {
            passcode = this._passcodeFactory.CreatePasscode();
        }

        Event @event = this._eventFactory.CreateEvent(eventName, guestId, organizerName, publicId, passcode?.Hash);
        await this._eventRepository.Save(@event);

        return new CreateEventResponse(
            @event.PublicId.Value,
            isPasscodeProtected,
            passcode?.Passcode.Value
        );
    }
}

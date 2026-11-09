public class CreateEvent
{
    private EventFactory _eventFactory;
    private EventRepository _eventRepository;
    private PublicIdGenerator _publicIdGenerator;
    private PasscodeFactory _passcodeFactory;
    private TimezoneConverter _timezoneConverter;

    public CreateEvent(
        EventFactory eventFactory,
        EventRepository eventRepository,
        PublicIdGenerator publicIdGenerator,
        PasscodeFactory passcodeFactory,
        TimezoneConverter timezoneConverter
    )
    {
        this._eventFactory = eventFactory;
        this._eventRepository = eventRepository;
        this._publicIdGenerator = publicIdGenerator;
        this._passcodeFactory = passcodeFactory;
        this._timezoneConverter = timezoneConverter;
    }

    public async Task<CreateEventResponse> Execute(
        string eventName,
        string guestId,
        string organizerName,
        bool isPasscodeProtected,
        string? timezone = null
    )
    {
        // The organizer's timezone becomes the event's anchor: the frame its
        // calendar hours and slot grid are defined in.
        if (timezone is not null)
            this._timezoneConverter.Resolve(timezone);

        PublicId publicId = this._publicIdGenerator.Generate();
        PasscodeCreated? passcode = null;
        if (isPasscodeProtected)
        {
            passcode = this._passcodeFactory.CreatePasscode();
        }

        Event @event = this._eventFactory.CreateEvent(eventName, guestId, organizerName, publicId, passcode?.Hash, timezone);
        await this._eventRepository.Save(@event);

        return new CreateEventResponse(
            @event.PublicId.Value,
            isPasscodeProtected,
            passcode?.Passcode.Value
        );
    }
}

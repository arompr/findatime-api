public class CreateEvent
{
    private EventFactory _eventFactory;
    private EventRepository _eventRepository;
    private PublicIdGenerator _publicIdGenerator;
    private PasscodeGenerator _passcodeGenerator;
    private PasscodeHasher _passcodeHasher;

    public CreateEvent(
        EventFactory eventFactory,
        EventRepository eventRepository,
        PublicIdGenerator publicIdGenerator,
        PasscodeGenerator passcodeGenerator,
        PasscodeHasher passcodeHasher)
    {
        this._eventFactory = eventFactory;
        this._eventRepository = eventRepository;
        this._publicIdGenerator = publicIdGenerator;
        this._passcodeGenerator = passcodeGenerator;
        this._passcodeHasher = passcodeHasher;
    }

    public async Task<CreateEventResponse> Execute(string eventName, string guestId, string organizerName, bool isPasscodeProtected)
    {
        PublicId publicId = this._publicIdGenerator.Generate();
        string? passcodeValue = null;
        PasscodeHash? passcodeHash = null;

        if (isPasscodeProtected)
        {
            Passcode passcode = this._passcodeGenerator.Generate();
            passcodeValue = passcode.Value;
            passcodeHash = this._passcodeHasher.Hash(passcode.Value);
        }

        Event domainEvent = this._eventFactory.CreateEvent(eventName, guestId, organizerName, publicId, passcodeHash);
        await this._eventRepository.Save(domainEvent);

        return new CreateEventResponse(domainEvent.Id.Value, domainEvent.PublicId.Value, isPasscodeProtected, passcodeValue);
    }
}

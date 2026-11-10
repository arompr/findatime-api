public class CreateGuest
{
    private GuestRepository _guestRepository;
    private GuestTokenFactory _tokenFactory;
    private TimeProvider _timeProvider;

    public CreateGuest(
        GuestRepository guestRepository,
        GuestTokenFactory tokenFactory,
        TimeProvider timeProvider
    )
    {
        this._guestRepository = guestRepository;
        this._tokenFactory = tokenFactory;
        this._timeProvider = timeProvider;
    }

    public Task<CreateGuestResult> Execute()
    {
        return this.CreateWithId(GuestId.FromString(Guid.NewGuid().ToString()));
    }

    internal async Task<CreateGuestResult> CreateWithId(GuestId id)
    {
        GuestTokenCreated token = this._tokenFactory.CreateToken();
        var guest = new Guest(id, token.Hash, this._timeProvider.GetUtcNow());
        await this._guestRepository.Save(guest);
        return new CreateGuestResult(token.Token);
    }
}

public class CreateGuest
{
    private GuestRepository _guestRepository;
    private GuestTokenGenerator _tokenGenerator;
    private GuestTokenHasher _tokenHasher;
    private TimeProvider _timeProvider;

    public CreateGuest(
        GuestRepository guestRepository,
        GuestTokenGenerator tokenGenerator,
        GuestTokenHasher tokenHasher,
        TimeProvider timeProvider
    )
    {
        this._guestRepository = guestRepository;
        this._tokenGenerator = tokenGenerator;
        this._tokenHasher = tokenHasher;
        this._timeProvider = timeProvider;
    }

    public Task<CreateGuestResult> Execute()
    {
        return this.CreateWithId(GuestId.FromString(Guid.NewGuid().ToString()));
    }

    internal async Task<CreateGuestResult> CreateWithId(GuestId id)
    {
        string token = this._tokenGenerator.Generate();
        var guest = new Guest(id, this._tokenHasher.Hash(token), this._timeProvider.GetUtcNow());
        await this._guestRepository.Save(guest);
        return new CreateGuestResult(token);
    }
}

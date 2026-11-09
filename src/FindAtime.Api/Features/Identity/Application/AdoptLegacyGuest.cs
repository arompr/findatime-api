using Npgsql;

public class AdoptLegacyGuest
{
    private GuestRepository _guestRepository;
    private CreateGuest _createGuest;

    public AdoptLegacyGuest(GuestRepository guestRepository, CreateGuest createGuest)
    {
        this._guestRepository = guestRepository;
        this._createGuest = createGuest;
    }

    public async Task<CreateGuestResult> Execute(Guid legacyGuestId)
    {
        GuestId id = GuestId.FromString(legacyGuestId.ToString());

        if (await this._guestRepository.Exists(id))
            throw new GuestAlreadyClaimedException(legacyGuestId);

        if (!await this._guestRepository.HasParticipations(id))
            return await this._createGuest.Execute();

        try
        {
            return await this._createGuest.CreateWithId(id);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new GuestAlreadyClaimedException(legacyGuestId);
        }
    }
}

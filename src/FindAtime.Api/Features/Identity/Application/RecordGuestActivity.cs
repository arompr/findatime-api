public class RecordGuestActivity
{
    private GuestRepository _guestRepository;
    private TimeProvider _timeProvider;

    public RecordGuestActivity(GuestRepository guestRepository, TimeProvider timeProvider)
    {
        this._guestRepository = guestRepository;
        this._timeProvider = timeProvider;
    }

    public async Task Execute(GuestId id)
    {
        Guest? guest = await this._guestRepository.GetById(id);
        if (guest is null)
            return;

        guest.MarkSeen(this._timeProvider.GetUtcNow());
        await this._guestRepository.Save(guest);
    }
}

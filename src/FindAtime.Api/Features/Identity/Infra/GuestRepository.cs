public class GuestRepository
{
    private DbContext _dbContext;

    public GuestRepository(DbContext dbContext)
    {
        this._dbContext = dbContext;
    }

    public async Task Save(Guest guest)
    {
        this._dbContext.Guests.Add(guest);
        await this._dbContext.SaveChangesAsync();
    }

    public Task<bool> Exists(GuestId id)
    {
        return this._dbContext.Guests.AnyAsync(g => g.Id == id);
    }

    public Task<bool> HasParticipations(GuestId id)
    {
        return this._dbContext.Participants.AnyAsync(p => p.GuestId == id);
    }

    public Task TouchLastSeen(GuestId id, DateTimeOffset seenAt)
    {
        return this._dbContext.Guests
            .Where(g => g.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.LastSeenAt, seenAt));
    }
}

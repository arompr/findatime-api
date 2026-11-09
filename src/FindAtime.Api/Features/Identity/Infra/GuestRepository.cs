public class GuestRepository
{
    private DbContext _dbContext;

    public GuestRepository(DbContext dbContext)
    {
        this._dbContext = dbContext;
    }

    public async Task Save(Guest guest)
    {
        if (this._dbContext.Entry(guest).State == EntityState.Detached)
            this._dbContext.Guests.Add(guest);

        await this._dbContext.SaveChangesAsync();
    }

    public Task<Guest?> GetById(GuestId id)
    {
        return this._dbContext.Guests.SingleOrDefaultAsync(g => g.Id == id);
    }

    public Task<bool> Exists(GuestId id)
    {
        return this._dbContext.Guests.AnyAsync(g => g.Id == id);
    }

    public Task<bool> HasParticipations(GuestId id)
    {
        return this._dbContext.Participants.AnyAsync(p => p.GuestId == id);
    }

}

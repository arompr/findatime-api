public class EventRepository
{
    private DbContext _dbContext;

    public EventRepository(DbContext dbContext)
    {
        this._dbContext = dbContext;
    }

    public async Task Save(Event domainEvent)
    {
        if (this._dbContext.Entry(domainEvent).State == EntityState.Detached)
            this._dbContext.Events.Add(domainEvent);

        await this._dbContext.SaveChangesAsync();
    }

    public async Task<Event?> GetById(Guid id)
    {
        List<Event> events = await this._dbContext.Events
            .Include(e => e.Participants)
            .Where(e => e.Id == new EventId(id.ToString()))
            .ToListAsync();

        return events.SingleOrDefault();
    }
}
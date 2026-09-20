using Microsoft.EntityFrameworkCore;

public class AvailabilityRepository
{
    private DbContext _dbContext;

    public AvailabilityRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task DeleteForParticipant(ParticipantId participantId)
    {
        List<Availability> existing = await _dbContext.Availabilities
            .Where(a => a.ParticipantId == participantId)
            .ToListAsync();

        _dbContext.Availabilities.RemoveRange(existing);
    }

    public async Task Save(IReadOnlyList<Availability> availabilities)
    {
        _dbContext.Availabilities.AddRange(availabilities);

        await _dbContext.SaveChangesAsync();
    }
}

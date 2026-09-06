class EventRepository
{
    private DbContext _dbContext;

    public EventRepository(DbContext dbContext)
    {
        this._dbContext = dbContext;
    }

    public async Task Save(Event domainEvent)
    {
        EventDbModel eventDbModel = new EventDbModel
        {
            Id = Guid.Parse(domainEvent.EventId.Value),
            Name = domainEvent.Name,
            CreatorParticipantId = Guid.Parse(domainEvent.CreatorParticipantId.Value),
        };

        this._dbContext.Events.Add(eventDbModel);

        foreach (Participant participant in domainEvent.Participants)
        {
            ParticipantDbModel participantDbModel = new ParticipantDbModel
            {
                Id = Guid.Parse(participant.ParticipantId.Value),
                ParticipantUuid = Guid.Parse(participant.ParticipantUuid.Value),
                EventId = Guid.Parse(domainEvent.EventId.Value),
            };

            this._dbContext.Participants.Add(participantDbModel);
        }

        await this._dbContext.SaveChangesAsync();
    }
}
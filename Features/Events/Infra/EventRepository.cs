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
            Id = Guid.Parse(domainEvent.Id.Value),
            Name = domainEvent.Name,
            OrganizerParticipantId = Guid.Parse(domainEvent.OrganizerParticipantId.Value),
            PublicId = domainEvent.PublicId.Value,
        };

        this._dbContext.Events.Add(eventDbModel);

        foreach (Participant participant in domainEvent.Participants)
        {
            ParticipantDbModel participantDbModel = new ParticipantDbModel
            {
                Id = Guid.Parse(participant.ParticipantId.Value),
                ParticipantUuid = Guid.Parse(participant.ParticipantUuid.Value),
                Name = participant.Name,
                EventId = Guid.Parse(domainEvent.Id.Value),
            };

            this._dbContext.Participants.Add(participantDbModel);
        }

        await this._dbContext.SaveChangesAsync();
    }
}

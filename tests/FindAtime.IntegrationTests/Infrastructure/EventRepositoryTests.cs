using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class EventRepositoryTests : IntegrationTest
{
    private EventFactory _eventFactory = default!;
    private PublicIdGenerator _publicIdGenerator = default!;
    private EventRepository _repository = default!;

    public EventRepositoryTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _eventFactory = Scope.ServiceProvider.GetRequiredService<EventFactory>();
        _publicIdGenerator = Scope.ServiceProvider.GetRequiredService<PublicIdGenerator>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
    }

    [Fact]
    public async Task Save_ThenGetById_ShouldRoundTrip()
    {
        var domainEvent = _eventFactory.CreateEvent(
            TestEvents.Name,
            TestEvents.OrganizerGuestId,
            TestEvents.OrganizerName,
            _publicIdGenerator.Generate(),
            PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]));

        await _repository.Save(domainEvent);

        var fetched = await _repository.GetById(Guid.Parse(domainEvent.Id.Value));

        Assert.NotNull(fetched);
        Assert.Equal(domainEvent.Id.Value, fetched.Id.Value);
        Assert.Equal(domainEvent.PublicId.Value, fetched.PublicId.Value);
        Assert.Equal(TestEvents.Name, fetched.Name);
        var participant = Assert.Single(fetched.Participants);
        Assert.Equal(domainEvent.OrganizerParticipantId.Value, participant.ParticipantId.Value);
    }

    [Fact]
    public async Task GetById_ShouldReturnNullForUnknownId()
    {
        var result = await _repository.GetById(Guid.NewGuid());

        Assert.Null(result);
    }
}

using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class CreateEventTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private EventRepository _repository = default!;

    public CreateEventTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
    }

    [Fact]
    public async Task Execute_ShouldPersistEventWithOrganizer()
    {
        var eventId = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerUuid, TestEvents.OrganizerName);

        var persisted = await _repository.GetById(Guid.Parse(eventId));

        Assert.NotNull(persisted);
        Assert.Equal(TestEvents.Name, persisted.Name);
        var organizer = Assert.Single(persisted.Participants);
        Assert.Equal(TestEvents.OrganizerUuid, organizer.ParticipantUuid.Value);
        Assert.Equal(TestEvents.OrganizerName, organizer.Name);
    }
}

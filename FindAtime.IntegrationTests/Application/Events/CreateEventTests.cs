using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class CreateEventTests
{
    private readonly PostgresFixture _fixture;

    public CreateEventTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Execute_ShouldPersistEventWithOrganizer()
    {
        var participantUuid = Guid.NewGuid().ToString();
        var eventId = "";

        await using (var provider = TestServices.Build(_fixture.ConnectionString))
        {
            await using var scope = provider.CreateAsyncScope();
            var createEvent = scope.ServiceProvider.GetRequiredService<CreateEvent>();
            eventId = await createEvent.Execute("Team Meeting", participantUuid, "Alice");
        }

        await using (var provider = TestServices.Build(_fixture.ConnectionString))
        {
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<EventRepository>();
            var persisted = await repository.GetById(Guid.Parse(eventId));

            Assert.NotNull(persisted);
            Assert.Equal("Team Meeting", persisted.Name);
            var organizer = Assert.Single(persisted.Participants);
            Assert.Equal(participantUuid, organizer.ParticipantUuid.Value);
            Assert.Equal("Alice", organizer.Name);
        }
    }
}
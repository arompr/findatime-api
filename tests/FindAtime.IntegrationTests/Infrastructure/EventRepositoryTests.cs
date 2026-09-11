using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class EventRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public EventRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Save_ThenGetById_ShouldRoundTrip()
    {
        var domainEvent = null as Event;

        await using (var provider = TestServices.Build(_fixture.ConnectionString))
        {
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<EventRepository>();
            domainEvent = CreateEvent(scope.ServiceProvider);
            await repository.Save(domainEvent);
        }

        await using (var provider = TestServices.Build(_fixture.ConnectionString))
        {
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<EventRepository>();
            var fetched = await repository.GetById(Guid.Parse(domainEvent.Id.Value));

            Assert.NotNull(fetched);
            Assert.Equal(domainEvent.Id.Value, fetched.Id.Value);
            Assert.Equal(domainEvent.PublicId.Value, fetched.PublicId.Value);
            Assert.Equal("Team Meeting", fetched.Name);
            var participant = Assert.Single(fetched.Participants);
            Assert.Equal(domainEvent.OrganizerParticipantId.Value, participant.ParticipantId.Value);
        }
    }

    [Fact]
    public async Task GetById_ShouldReturnNullForUnknownId()
    {
        await using var provider = TestServices.Build(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();

        var repository = scope.ServiceProvider.GetRequiredService<EventRepository>();
        var result = await repository.GetById(Guid.NewGuid());

        Assert.Null(result);
    }

    private static Event CreateEvent(IServiceProvider services)
    {
        var eventFactory = services.GetRequiredService<EventFactory>();
        var publicIdGenerator = services.GetRequiredService<PublicIdGenerator>();
        return eventFactory.CreateEvent("Team Meeting", Guid.NewGuid().ToString(), "Alice", publicIdGenerator.Generate());
    }
}
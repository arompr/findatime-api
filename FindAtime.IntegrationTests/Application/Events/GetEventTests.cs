using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetEventTests
{
    private readonly PostgresFixture _fixture;

    public GetEventTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Execute_ShouldReturnEventDto()
    {
        await using var provider = TestServices.Build(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();

        var eventFactory = scope.ServiceProvider.GetRequiredService<EventFactory>();
        var publicIdGenerator = scope.ServiceProvider.GetRequiredService<PublicIdGenerator>();
        var repository = scope.ServiceProvider.GetRequiredService<EventRepository>();

        var domainEvent = eventFactory.CreateEvent("Team Meeting", Guid.NewGuid().ToString(), "Alice", publicIdGenerator.Generate());
        await repository.Save(domainEvent);

        var getEvent = scope.ServiceProvider.GetRequiredService<GetEvent>();
        var dto = await getEvent.Execute(Guid.Parse(domainEvent.Id.Value));

        Assert.NotNull(dto);
        Assert.Equal(domainEvent.Id.Value, dto.Id);
        Assert.Equal("Team Meeting", dto.Name);
    }

    [Fact]
    public async Task Execute_ShouldReturnNullForUnknownId()
    {
        await using var provider = TestServices.Build(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();

        var getEvent = scope.ServiceProvider.GetRequiredService<GetEvent>();
        var dto = await getEvent.Execute(Guid.NewGuid());

        Assert.Null(dto);
    }
}

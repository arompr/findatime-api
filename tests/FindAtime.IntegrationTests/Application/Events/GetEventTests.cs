using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetEventTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private GetEvent _getEvent = default!;

    public GetEventTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _getEvent = Scope.ServiceProvider.GetRequiredService<GetEvent>();
    }

    [Fact]
    public async Task Execute_ShouldReturnEventDto()
    {
        var eventId = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerUuid, TestEvents.OrganizerName);

        var dto = await _getEvent.Execute(Guid.Parse(eventId));

        Assert.NotNull(dto);
        Assert.Equal(eventId, dto.Id);
        Assert.Equal(TestEvents.Name, dto.Name);
    }

    [Fact]
    public async Task Execute_ShouldReturnNullForUnknownId()
    {
        var dto = await _getEvent.Execute(Guid.NewGuid());

        Assert.Null(dto);
    }
}

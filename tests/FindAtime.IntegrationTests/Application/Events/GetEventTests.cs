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
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var dto = await _getEvent.Execute(Guid.Parse(response.EventId));

        Assert.NotNull(dto);
        Assert.Equal(response.EventId, dto.Id);
        Assert.Equal(TestEvents.Name, dto.Name);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownId()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _getEvent.Execute(Guid.NewGuid()));
    }
}

using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetEventByPublicIdTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private GetEventByPublicId _getEventByPublicId = default!;

    public GetEventByPublicIdTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _getEventByPublicId = Scope.ServiceProvider.GetRequiredService<GetEventByPublicId>();
    }

    [Fact]
    public async Task Execute_ShouldReturnEventByPublicId()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var dto = await _getEventByPublicId.Execute(response.PublicId);

        Assert.NotNull(dto);
        Assert.Equal(response.EventId, dto.EventId);
        Assert.Equal(TestEvents.Name, dto.Name);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownPublicId()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _getEventByPublicId.Execute("nonexistent123"));
    }
}

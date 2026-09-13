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
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        var dto = await _getEvent.Execute(response.PublicId);

        Assert.NotNull(dto);
        Assert.Equal(response.PublicId, dto.PublicId);
        Assert.Equal(TestEvents.Name, dto.Name);
        Assert.True(dto.IsPasscodeProtected);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownPublicId()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _getEvent.Execute("nonexistent123"));
    }
}

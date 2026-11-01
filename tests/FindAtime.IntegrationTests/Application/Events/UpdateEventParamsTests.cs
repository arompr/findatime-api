using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class UpdateEventParamsTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private UpdateEventParams _updateEventParams = default!;
    private EventRepository _repository = default!;

    public UpdateEventParamsTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _updateEventParams = Scope.ServiceProvider.GetRequiredService<UpdateEventParams>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
    }

    private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task Execute_ShouldEnableTimezoneForOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30);

        Assert.Equal("Europe/Berlin", response.Timezone);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal("Europe/Berlin", persisted!.Params.Timezone);
    }

    [Fact]
    public async Task Execute_ShouldChangeTimezoneForOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Asia/Tokyo", 30);

        Assert.Equal("Asia/Tokyo", response.Timezone);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal("Asia/Tokyo", persisted!.Params.Timezone);
    }

    [Fact]
    public async Task Execute_ShouldDisableTimezoneForOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 30);

        Assert.Null(response.Timezone);
        Assert.Equal(30, response.IntervalMinutes);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Null(persisted!.Params.Timezone);
        Assert.Equal(30, persisted.Params.IntervalMinutes);
    }

    [Fact]
    public async Task Execute_ShouldThrowForNonOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ForbiddenException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(FriendGuestId), "Europe/Berlin", 30));
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _updateEventParams.Execute(
            "no-such-public-id", Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30));
    }

    [Fact]
    public async Task Execute_ShouldThrowForInvalidTimezone()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidTimezoneException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Not/A_Timezone", 30));
    }

    [Fact]
    public async Task Execute_ShouldPersistIntervalMinutes()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 15);

        Assert.Equal("Europe/Berlin", response.Timezone);
        Assert.Equal(15, response.IntervalMinutes);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal(15, persisted!.Params.IntervalMinutes);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    public async Task Execute_ShouldAcceptEachAllowedValue(int intervalMinutes)
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", intervalMinutes);

        Assert.Equal("Europe/Berlin", response.Timezone);
        Assert.Equal(intervalMinutes, response.IntervalMinutes);
    }

    [Fact]
    public async Task Execute_ShouldThrowForInvalidInterval()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidIntervalException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 45));
    }

    [Fact]
    public async Task Execute_ShouldValidateIntervalBeforeTimezone()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidIntervalException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Not/A_Timezone", 45));
    }
}

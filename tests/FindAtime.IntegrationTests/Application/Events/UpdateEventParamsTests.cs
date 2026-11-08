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
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30, 9, 21);

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
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30, 9, 21);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Asia/Tokyo", 30, 9, 21);

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
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30, 9, 21);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 30, 9, 21);

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
            created.PublicId, Guid.Parse(FriendGuestId), "Europe/Berlin", 30, 9, 21));
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _updateEventParams.Execute(
            "no-such-public-id", Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30, 9, 21));
    }

    [Fact]
    public async Task Execute_ShouldThrowForInvalidTimezone()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidTimezoneException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Not/A_Timezone", 30, 9, 21));
    }

    [Fact]
    public async Task Execute_ShouldPersistIntervalMinutes()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 15, 9, 21);

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
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", intervalMinutes, 9, 21);

        Assert.Equal("Europe/Berlin", response.Timezone);
        Assert.Equal(intervalMinutes, response.IntervalMinutes);
    }

    [Fact]
    public async Task Execute_ShouldThrowForInvalidInterval()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidIntervalException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 45, 9, 21));
    }

    [Fact]
    public async Task Execute_ShouldValidateIntervalBeforeTimezone()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidIntervalException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Not/A_Timezone", 45, 9, 21));
    }

    [Fact]
    public async Task Execute_ShouldReplaceDayWindow()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Europe/Berlin", 30, 9, 17);

        Assert.Equal(9, response.DayStartHour);
        Assert.Equal(17, response.DayEndHour);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal(9, persisted!.Params.DayStartHour);
        Assert.Equal(17, persisted.Params.DayEndHour);
    }

    [Fact]
    public async Task Execute_ShouldAcceptFullDayWindow()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var response = await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 30, 0, 24);

        Assert.Equal(0, response.DayStartHour);
        Assert.Equal(24, response.DayEndHour);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal(0, persisted!.Params.DayStartHour);
        Assert.Equal(24, persisted.Params.DayEndHour);
    }

    [Theory]
    [InlineData(17, 9)]
    [InlineData(10, 10)]
    [InlineData(-1, 9)]
    [InlineData(9, 25)]
    [InlineData(24, 24)]
    public async Task Execute_ShouldThrowForInvalidDayWindowAndKeepStoredWindow(int startHour, int endHour)
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidDayWindowException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 30, startHour, endHour));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal(9, persisted!.Params.DayStartHour);
        Assert.Equal(21, persisted.Params.DayEndHour);
    }

    [Fact]
    public async Task Execute_ShouldValidateIntervalBeforeDayWindow()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidIntervalException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 20, 17, 9));
    }

    [Fact]
    public async Task Execute_ShouldValidateDayWindowBeforeTimezone()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<InvalidDayWindowException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Not/A_Timezone", 30, 17, 9));
    }

    [Fact]
    public async Task Execute_ShouldNotChangeDayWindowForNonOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ForbiddenException>(() => _updateEventParams.Execute(
            created.PublicId, Guid.Parse(FriendGuestId), null, 30, 10, 19));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal(9, persisted!.Params.DayStartHour);
        Assert.Equal(21, persisted.Params.DayEndHour);
    }
}

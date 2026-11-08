using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetEventTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private ReadEventService _readEventService = default!;

    public GetEventTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _readEventService = Scope.ServiceProvider.GetRequiredService<ReadEventService>();
    }

    [Fact]
    public async Task GetEventByPublicId_ShouldReturnEventDto()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        var dto = await _readEventService.GetEventByPublicId(response.PublicId);

        Assert.NotNull(dto);
        Assert.Equal(response.PublicId, dto.PublicId);
        Assert.Equal(TestEvents.Name, dto.Name);
        Assert.True(dto.IsPasscodeProtected);
        Assert.Equal(30, dto.IntervalMinutes);
        Assert.Equal(9, dto.DayStartHour);
        Assert.Equal(21, dto.DayEndHour);
    }

    [Fact]
    public async Task GetEventByPublicId_ShouldReturnUpdatedDayWindow()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);
        var updateEventParams = Scope.ServiceProvider.GetRequiredService<UpdateEventParams>();
        await updateEventParams.Execute(
            response.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 30, 10, 19);

        var dto = await _readEventService.GetEventByPublicId(response.PublicId);

        Assert.NotNull(dto);
        Assert.Equal(10, dto.DayStartHour);
        Assert.Equal(19, dto.DayEndHour);
    }

    [Fact]
    public async Task GetEventByPublicId_ShouldReturnNullForUnknownPublicId()
    {
        var dto = await _readEventService.GetEventByPublicId("nonexistent123");

        Assert.Null(dto);
    }
}

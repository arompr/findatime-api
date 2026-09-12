using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class SearchEventsTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private LeaveEvent _leaveEvent = default!;
    private SearchEvents _searchEvents = default!;

    public SearchEventsTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _leaveEvent = Scope.ServiceProvider.GetRequiredService<LeaveEvent>();
        _searchEvents = Scope.ServiceProvider.GetRequiredService<SearchEvents>();
    }

    private const string OtherGuestId = "22222222-2222-2222-2222-222222222222";
    private const string OtherName = "Bob";

    [Fact]
    public async Task Execute_ShouldReturnAllEventsOrganizedByGuest()
    {
        var first = await _createEvent.Execute(
            "First", TestEvents.OrganizerGuestId, TestEvents.OrganizerName);
        var second = await _createEvent.Execute(
            "Second", TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var results = await _searchEvents.Execute(Guid.Parse(TestEvents.OrganizerGuestId));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.PublicId == first.PublicId && r.Name == "First");
        Assert.Contains(results, r => r.PublicId == second.PublicId && r.Name == "Second");
    }

    [Fact]
    public async Task Execute_ShouldReturnAllEventsJoinedByGuest()
    {
        var first = await _createEvent.Execute(
            "First", TestEvents.OrganizerGuestId, TestEvents.OrganizerName);
        var second = await _createEvent.Execute(
            "Second", TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await _joinEvent.Execute(Guid.Parse(first.EventId), first.Passcode, OtherGuestId, OtherName);
        await _joinEvent.Execute(Guid.Parse(second.EventId), second.Passcode, OtherGuestId, OtherName);

        var results = await _searchEvents.Execute(Guid.Parse(OtherGuestId));

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Execute_ShouldReturnEventsAcrossOrganizerAndParticipantRoles()
    {
        var organized = await _createEvent.Execute(
            "Organized", TestEvents.OrganizerGuestId, TestEvents.OrganizerName);
        var joined = await _createEvent.Execute(
            "Joined", OtherGuestId, OtherName);

        await _joinEvent.Execute(Guid.Parse(joined.EventId), joined.Passcode, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var results = await _searchEvents.Execute(Guid.Parse(TestEvents.OrganizerGuestId));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.PublicId == organized.PublicId);
        Assert.Contains(results, r => r.PublicId == joined.PublicId);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyListForUnknownGuest()
    {
        var results = await _searchEvents.Execute(Guid.NewGuid());

        Assert.Empty(results);
    }

    [Fact]
    public async Task Execute_ShouldNotReturnEventGuestLeft()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);
        await _joinEvent.Execute(Guid.Parse(created.EventId), created.Passcode, OtherGuestId, OtherName);

        await _leaveEvent.Execute(Guid.Parse(created.EventId), OtherGuestId);

        var results = await _searchEvents.Execute(Guid.Parse(OtherGuestId));

        Assert.Empty(results);
    }

    [Fact]
    public async Task Execute_ShouldNotReturnOtherGuestsEvents()
    {
        await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var results = await _searchEvents.Execute(Guid.Parse(OtherGuestId));

        Assert.Empty(results);
    }
}

using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetMyParticipantTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private GetMyParticipant _getMyParticipant = default!;

    public GetMyParticipantTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _getMyParticipant = Scope.ServiceProvider.GetRequiredService<GetMyParticipant>();
    }

    private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";
    private const string FriendName = "Bob";

    [Fact]
    public async Task Execute_ShouldResolveJoinedParticipant()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName);

        var result = await _getMyParticipant.Execute(created.PublicId, Guid.Parse(FriendGuestId));

        Assert.Equal(joined.ParticipantId, result.ParticipantId);
        Assert.Equal(FriendName, result.Name);
    }

    [Fact]
    public async Task Execute_ShouldResolveOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var result = await _getMyParticipant.Execute(created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId));

        Assert.Equal(TestEvents.OrganizerName, result.Name);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _getMyParticipant.Execute(
            "no-such-public-id", Guid.Parse(FriendGuestId)));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenGuestHasNotJoined()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _getMyParticipant.Execute(
            created.PublicId, Guid.Parse(FriendGuestId)));
    }
}

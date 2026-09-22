using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class JoinEventTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private EventRepository _repository = default!;

    public JoinEventTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
    }

    private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";
    private const string FriendName = "Bob";

    [Fact]
    public async Task Execute_ShouldJoinNewParticipant()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        var response = await _joinEvent.Execute(
            created.PublicId, created.Passcode, FriendGuestId, FriendName);

        Assert.Equal(FriendName, response.Name);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Participants.Count);
    }

    [Fact]
    public async Task Execute_ShouldJoinUnprotectedEventWithoutPasscode()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var response = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName);

        Assert.Equal(FriendName, response.Name);

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Participants.Count);
    }

    [Fact]
    public async Task Execute_ShouldThrowForWrongPasscode()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        await Assert.ThrowsAsync<InvalidPasscodeException>(() => _joinEvent.Execute(
            created.PublicId, "WRONG6", FriendGuestId, FriendName));
    }

    [Fact]
    public async Task Execute_ShouldThrowForMissingPasscodeOnProtectedEvent()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        await Assert.ThrowsAsync<InvalidPasscodeException>(() => _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenRejoiningWithSameName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        await _joinEvent.Execute(created.PublicId, created.Passcode, FriendGuestId, FriendName);

        await Assert.ThrowsAsync<ParticipantAlreadyJoinedException>(() => _joinEvent.Execute(
            created.PublicId, created.Passcode, FriendGuestId, FriendName));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Equal(2, persisted!.Participants.Count);
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenRejoiningWithDifferentName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        await _joinEvent.Execute(created.PublicId, created.Passcode, FriendGuestId, FriendName);

        await Assert.ThrowsAsync<ParticipantAlreadyJoinedException>(() => _joinEvent.Execute(
            created.PublicId, created.Passcode, FriendGuestId, "Robert"));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenOrganizerTriesToJoin()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        await Assert.ThrowsAsync<ParticipantAlreadyJoinedException>(() => _joinEvent.Execute(
            created.PublicId, created.Passcode, TestEvents.OrganizerGuestId, TestEvents.OrganizerName));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Single(persisted!.Participants);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _joinEvent.Execute(
            "nonexistent1", "ABC234", FriendGuestId, FriendName));
    }
}

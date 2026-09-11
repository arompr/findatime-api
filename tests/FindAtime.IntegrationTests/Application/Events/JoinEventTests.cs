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
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var response = await _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName);

        Assert.Equal(FriendName, response.Name);

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Participants.Count);
    }

    [Fact]
    public async Task Execute_ShouldThrowForWrongPasscode()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await Assert.ThrowsAsync<InvalidPasscodeException>(() => _joinEvent.Execute(
            Guid.Parse(created.EventId), "WRONG6", FriendGuestId, FriendName));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenRejoiningWithSameName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await _joinEvent.Execute(Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName);

        await Assert.ThrowsAsync<ParticipantAlreadyJoinedException>(() => _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName));

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Equal(2, persisted!.Participants.Count);
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenRejoiningWithDifferentName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await _joinEvent.Execute(Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName);

        await Assert.ThrowsAsync<ParticipantAlreadyJoinedException>(() => _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, FriendGuestId, "Robert"));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenOrganizerTriesToJoin()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await Assert.ThrowsAsync<ParticipantAlreadyJoinedException>(() => _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, TestEvents.OrganizerGuestId, TestEvents.OrganizerName));

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Single(persisted!.Participants);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _joinEvent.Execute(
            Guid.NewGuid(), "ABC234", FriendGuestId, FriendName));
    }
}

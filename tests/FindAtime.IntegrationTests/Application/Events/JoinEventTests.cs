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

        var result = await _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName);

        Assert.Equal(JoinEventStatus.Joined, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(FriendName, result.Response!.Name);

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Participants.Count);
    }

    [Fact]
    public async Task Execute_ShouldRejectWrongPasscode()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var result = await _joinEvent.Execute(
            Guid.Parse(created.EventId), "WRONG6", FriendGuestId, FriendName);

        Assert.Equal(JoinEventStatus.InvalidPasscode, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task Execute_ShouldReturnAlreadyJoinedWhenRejoiningWithSameName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var first = await _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName);
        var second = await _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName);

        Assert.Equal(JoinEventStatus.AlreadyJoined, second.Status);
        Assert.Equal(first.Response!.ParticipantId, second.Response!.ParticipantId);

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Equal(2, persisted!.Participants.Count);
    }

    [Fact]
    public async Task Execute_ShouldRejectRejoinWithDifferentName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await _joinEvent.Execute(Guid.Parse(created.EventId), created.Passcode, FriendGuestId, FriendName);
        var result = await _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, FriendGuestId, "Robert");

        Assert.Equal(JoinEventStatus.NameMismatch, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task Execute_ShouldReturnAlreadyJoinedForOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var result = await _joinEvent.Execute(
            Guid.Parse(created.EventId), created.Passcode, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        Assert.Equal(JoinEventStatus.AlreadyJoined, result.Status);

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Single(persisted!.Participants);
    }

    [Fact]
    public async Task Execute_ShouldReturnEventNotFoundForUnknownEvent()
    {
        var result = await _joinEvent.Execute(Guid.NewGuid(), "ABC234", FriendGuestId, FriendName);

        Assert.Equal(JoinEventStatus.EventNotFound, result.Status);
    }
}

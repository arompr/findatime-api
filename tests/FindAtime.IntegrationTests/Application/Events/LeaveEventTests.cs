using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class LeaveEventTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private LeaveEvent _leaveEvent = default!;
    private EventRepository _repository = default!;

    public LeaveEventTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _leaveEvent = Scope.ServiceProvider.GetRequiredService<LeaveEvent>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
    }

    private const string FriendGuestId = "33333333-3333-3333-3333-333333333333";

    [Fact]
    public async Task Execute_ShouldRemoveParticipant()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);
        await _joinEvent.Execute(Guid.Parse(created.EventId), created.Passcode, FriendGuestId, "Bob");

        await _leaveEvent.Execute(Guid.Parse(created.EventId), FriendGuestId);

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Single(persisted!.Participants);
    }

    [Fact]
    public async Task Execute_ShouldBeIdempotentWhenAlreadyLeft()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);
        await _joinEvent.Execute(Guid.Parse(created.EventId), created.Passcode, FriendGuestId, "Bob");

        await _leaveEvent.Execute(Guid.Parse(created.EventId), FriendGuestId);
        await _leaveEvent.Execute(Guid.Parse(created.EventId), FriendGuestId);

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Single(persisted!.Participants);
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenOrganizerLeaves()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await Assert.ThrowsAsync<OrganizerCannotLeaveException>(() => _leaveEvent.Execute(
            Guid.Parse(created.EventId), TestEvents.OrganizerGuestId));

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Single(persisted!.Participants);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _leaveEvent.Execute(Guid.NewGuid(), FriendGuestId));
    }

    [Fact]
    public async Task Execute_ShouldBeNoOpForGuestNotInEvent()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        await _leaveEvent.Execute(Guid.Parse(created.EventId), FriendGuestId);

        var persisted = await _repository.GetById(Guid.Parse(created.EventId));
        Assert.Single(persisted!.Participants);
    }
}

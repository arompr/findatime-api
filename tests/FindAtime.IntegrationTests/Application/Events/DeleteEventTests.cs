using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class DeleteEventTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private SetAvailability _setAvailability = default!;
    private DeleteEvent _deleteEvent = default!;
    private EventRepository _repository = default!;

    public DeleteEventTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _setAvailability = Scope.ServiceProvider.GetRequiredService<SetAvailability>();
        _deleteEvent = Scope.ServiceProvider.GetRequiredService<DeleteEvent>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
    }

    private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task Execute_ShouldDeleteEventWithParticipants()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);
        await _joinEvent.Execute(created.PublicId, created.Passcode, FriendGuestId, "Bob", "UTC");

        await _deleteEvent.Execute(created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Null(persisted);
    }

    [Fact]
    public async Task Execute_ShouldDeleteAvailabilitiesWithParticipants()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);
        var joined = await _joinEvent.Execute(created.PublicId, created.Passcode, FriendGuestId, "Bob", "UTC");

        var start = new DateTime(2026, 9, 19, 9, 0, 0);
        var end = new DateTime(2026, 9, 19, 10, 0, 0);

        await _setAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), Guid.Parse(FriendGuestId), "UTC",
            [new AvailabilityRangeResult(start, end)]);

        await _deleteEvent.Execute(created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId));

        Assert.Null(await _repository.GetByPublicId(created.PublicId));

        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        var any = await db.Availabilities
            .AnyAsync(a => a.ParticipantId == ParticipantId.FromString(joined.ParticipantId));
        Assert.False(any);
    }

    [Fact]
    public async Task Execute_ShouldThrowForNonOrganizer()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);
        await _joinEvent.Execute(created.PublicId, created.Passcode, FriendGuestId, "Bob", "UTC");

        await Assert.ThrowsAsync<ForbiddenException>(() => _deleteEvent.Execute(
            created.PublicId, Guid.Parse(FriendGuestId)));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task Execute_ShouldThrowForStranger()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ForbiddenException>(() => _deleteEvent.Execute(
            created.PublicId, Guid.Parse("99999999-9999-9999-9999-999999999999")));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _deleteEvent.Execute(
            "nonexistent1", Guid.Parse(TestEvents.OrganizerGuestId)));
    }

    [Fact]
    public async Task Execute_ShouldThrowForAlreadyDeletedEvent()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _deleteEvent.Execute(created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId));

        await Assert.ThrowsAsync<EventNotFoundException>(() => _deleteEvent.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId)));
    }

    [Fact]
    public async Task Execute_ShouldDeletePasscodeProtectedEvent()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        await _deleteEvent.Execute(created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId));

        var persisted = await _repository.GetByPublicId(created.PublicId);
        Assert.Null(persisted);
    }
}

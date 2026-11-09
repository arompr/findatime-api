using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetParticipantAvailabilityTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private UpdateEventParams _updateEventParams = default!;
    private GetParticipantAvailability _getParticipantAvailability = default!;

    public GetParticipantAvailabilityTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _updateEventParams = Scope.ServiceProvider.GetRequiredService<UpdateEventParams>();
        _getParticipantAvailability = Scope.ServiceProvider.GetRequiredService<GetParticipantAvailability>();
    }

    private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";
    private const string FriendName = "Bob";

    private async Task SeedAvailability(string participantId, params (DateTimeOffset Start, DateTimeOffset End)[] ranges)
    {
        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        foreach (var (start, end) in ranges)
        {
            db.Availabilities.Add(new Availability(
                AvailabilityId.FromString(Guid.NewGuid().ToString()),
                ParticipantId.FromString(participantId),
                start,
                end,
                DateTimeOffset.UtcNow
            ));
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyRangesWhenNone()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        var result = await _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid);

        Assert.Equal(joined.ParticipantId, result.ParticipantId);
        Assert.Equal(FriendName, result.Name);
        Assert.Empty(result.Ranges);
    }

    [Fact]
    public async Task Execute_ShouldReturnRangesOrderedByStart()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        var late = new DateTimeOffset(2026, 9, 19, 14, 0, 0, TimeSpan.Zero);
        var early = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);

        await SeedAvailability(joined.ParticipantId, (late, late.AddHours(1)), (early, early.AddHours(1)));

        var result = await _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid);
        Assert.Equal(2, result.Ranges.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero), result.Ranges[0].Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 14, 0, 0, TimeSpan.Zero), result.Ranges[1].Start);
    }

    [Fact]
    public async Task Execute_ShouldReturnUtcInstants()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "America/New_York", 30, 9, 21);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        await SeedAvailability(joined.ParticipantId, (
            new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 3, 18, 0, 0, TimeSpan.Zero)));

        var result = await _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid);

        var range = Assert.Single(result.Ranges);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 18, 0, 0, TimeSpan.Zero), range.End);
        Assert.Equal(TimeSpan.Zero, range.Start.Offset);
    }

    [Fact]
    public async Task Execute_ShouldAnchorLegacyEventWithoutTimezoneToUtc()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 60, 9, 21);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Asia/Kolkata");

        await SeedAvailability(joined.ParticipantId, (
            new DateTimeOffset(2026, 9, 19, 3, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 19, 5, 30, 0, TimeSpan.Zero)));

        var result = await _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid);

        var range = Assert.Single(result.Ranges);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 4, 0, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 5, 0, 0, TimeSpan.Zero), range.End);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _getParticipantAvailability.Execute(
            "no-such-public-id", Guid.NewGuid(), TestEvents.OrganizerGuid));
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownParticipant()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _getParticipantAvailability.Execute(
            created.PublicId, Guid.NewGuid(), TestEvents.OrganizerGuid));
    }

    [Fact]
    public async Task Execute_ShouldThrowForCrossEventParticipant()
    {
        var first = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            first.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        var second = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _getParticipantAvailability.Execute(
            second.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid));
    }

    [Fact]
    public async Task Execute_ShouldProjectRangesOntoCurrentInterval()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 60, 9, 21);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "UTC");

        await SeedAvailability(joined.ParticipantId, (
            new DateTimeOffset(2026, 9, 19, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero)));

        var result = await _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid);

        var range = Assert.Single(result.Ranges);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero), range.End);
    }

    [Fact]
    public async Task Execute_ShouldOmitRangesWithoutFullInterval()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 60, 9, 21);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "UTC");

        await SeedAvailability(joined.ParticipantId, (
            new DateTimeOffset(2026, 9, 19, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 19, 9, 30, 0, TimeSpan.Zero)));

        var result = await _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid);

        Assert.Empty(result.Ranges);
    }

    [Fact]
    public async Task Execute_ShouldAnchorGridOnEventTimezone()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "Asia/Kolkata", 60, 9, 21);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Asia/Kolkata");

        await SeedAvailability(joined.ParticipantId, (
            new DateTimeOffset(2026, 9, 19, 3, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 19, 4, 45, 0, TimeSpan.Zero)));

        var result = await _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), TestEvents.OrganizerGuid);

        // 09:00-10:00 IST
        var range = Assert.Single(result.Ranges);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 3, 30, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 4, 30, 0, TimeSpan.Zero), range.End);
    }

    [Fact]
    public async Task Execute_ShouldForbidGuestWhoIsNotAParticipant()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);
        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        await Assert.ThrowsAsync<ForbiddenException>(() => _getParticipantAvailability.Execute(
            created.PublicId, Guid.Parse(joined.ParticipantId), Guid.NewGuid()));
    }
}

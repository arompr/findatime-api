using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetEventAvailabilitiesTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private GetEventAvailabilities _getEventAvailabilities = default!;

    public GetEventAvailabilitiesTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _getEventAvailabilities = Scope.ServiceProvider.GetRequiredService<GetEventAvailabilities>();
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
    public async Task Execute_ShouldReturnParticipantsWithEmptyRangesWhenNoAvailability()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _joinEvent.Execute(
            Guid.Parse(created.EventId), null, FriendGuestId, FriendName);

        var result = await _getEventAvailabilities.Execute(created.PublicId);

        Assert.Null(result.EventTimezone);
        Assert.Equal(2, result.Participants.Count);
        Assert.All(result.Participants, p => Assert.Empty(p.Ranges));
    }

    [Fact]
    public async Task Execute_ShouldGroupRangesByParticipantAndOrderByStart()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            Guid.Parse(created.EventId), null, FriendGuestId, FriendName);

        var late = new DateTimeOffset(2026, 9, 19, 14, 0, 0, TimeSpan.Zero);
        var early = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);

        await SeedAvailability(joined.ParticipantId, (late, late.AddHours(1)), (early, early.AddHours(1)));

        var result = await _getEventAvailabilities.Execute(created.PublicId);

        var friend = result.Participants.Single(p => p.ParticipantId == joined.ParticipantId);
        Assert.Equal(2, friend.Ranges.Count);
        Assert.Equal(early, friend.Ranges[0].Start);
        Assert.Equal(late, friend.Ranges[1].Start);

        var organizer = result.Participants.Single(p => p.Name == TestEvents.OrganizerName);
        Assert.Empty(organizer.Ranges);
    }

    [Fact]
    public async Task Execute_ShouldOrderParticipantsByName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _joinEvent.Execute(
            Guid.Parse(created.EventId), null, FriendGuestId, FriendName);

        var result = await _getEventAvailabilities.Execute(created.PublicId);

        Assert.Equal(TestEvents.OrganizerName, result.Participants[0].Name);
        Assert.Equal(FriendName, result.Participants[1].Name);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _getEventAvailabilities.Execute(
            "no-such-public-id"));
    }
}

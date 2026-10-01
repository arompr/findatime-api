using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class GetEventAvailabilitiesTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private UpdateEventParams _updateEventParams = default!;
    private GetEventAvailabilities _getEventAvailabilities = default!;

    public GetEventAvailabilitiesTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _updateEventParams = Scope.ServiceProvider.GetRequiredService<UpdateEventParams>();
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
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        var result = await _getEventAvailabilities.Execute(created.PublicId, null);

        Assert.Equal("UTC", result.Timezone);
        Assert.Equal(2, result.Participants.Count);
        Assert.All(result.Participants, p => Assert.Empty(p.Ranges));
    }

    [Fact]
    public async Task Execute_ShouldGroupRangesByParticipantAndOrderByStart()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        var late = new DateTimeOffset(2026, 9, 19, 14, 0, 0, TimeSpan.Zero);
        var early = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);

        await SeedAvailability(joined.ParticipantId, (late, late.AddHours(1)), (early, early.AddHours(1)));

        var result = await _getEventAvailabilities.Execute(created.PublicId, null);

        Assert.Equal("UTC", result.Timezone);

        var friend = result.Participants.Single(p => p.ParticipantId == joined.ParticipantId);
        Assert.Equal(2, friend.Ranges.Count);
        Assert.Equal(new DateTime(2026, 9, 19, 9, 0, 0), friend.Ranges[0].Start);
        Assert.Equal(new DateTime(2026, 9, 19, 14, 0, 0), friend.Ranges[1].Start);

        var organizer = result.Participants.Single(p => p.Name == TestEvents.OrganizerName);
        Assert.Empty(organizer.Ranges);
    }

    [Fact]
    public async Task Execute_ShouldOrderParticipantsByName()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        var result = await _getEventAvailabilities.Execute(created.PublicId, null);

        Assert.Equal(TestEvents.OrganizerName, result.Participants[0].Name);
        Assert.Equal(FriendName, result.Participants[1].Name);
    }

    [Fact]
    public async Task Execute_ShouldConvertTimesAndLabelWhenOptionOn()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "America/New_York");

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        await SeedAvailability(joined.ParticipantId, (
            new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 19, 13, 0, 0, TimeSpan.Zero)));

        var result = await _getEventAvailabilities.Execute(created.PublicId, "Asia/Tokyo");

        Assert.Equal("Asia/Tokyo", result.Timezone);

        var friend = result.Participants.Single(p => p.ParticipantId == joined.ParticipantId);
        var range = Assert.Single(friend.Ranges);
        Assert.Equal(new DateTime(2026, 9, 19, 21, 0, 0), range.Start);
    }

    [Fact]
    public async Task Execute_ShouldFallbackViewerToEventToUtc()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await _updateEventParams.Execute(
            created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), "America/New_York");

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, "Europe/Berlin");

        await SeedAvailability(joined.ParticipantId, (
            new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 19, 13, 0, 0, TimeSpan.Zero)));

        var withViewer = await _getEventAvailabilities.Execute(created.PublicId, "Asia/Tokyo");
        Assert.Equal("Asia/Tokyo", withViewer.Timezone);
        var viewerRange = Assert.Single(
            withViewer.Participants.Single(p => p.ParticipantId == joined.ParticipantId).Ranges);
        Assert.Equal(new DateTime(2026, 9, 19, 21, 0, 0), viewerRange.Start);

        var withEventFallback = await _getEventAvailabilities.Execute(created.PublicId, null);
        Assert.Equal("America/New_York", withEventFallback.Timezone);
        var eventRange = Assert.Single(
            withEventFallback.Participants.Single(p => p.ParticipantId == joined.ParticipantId).Ranges);
        Assert.Equal(new DateTime(2026, 9, 19, 8, 0, 0), eventRange.Start);
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _getEventAvailabilities.Execute(
            "no-such-public-id", null));
    }
}

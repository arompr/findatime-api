using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class SetAvailabilityTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private SetAvailability _setAvailability = default!;
    private GetParticipantAvailability _getParticipantAvailability = default!;
    private GetEventAvailabilities _getEventAvailabilities = default!;
    private UpdateEventParams _updateEventParams = default!;

    public SetAvailabilityTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _setAvailability = Scope.ServiceProvider.GetRequiredService<SetAvailability>();
        _getParticipantAvailability = Scope.ServiceProvider.GetRequiredService<GetParticipantAvailability>();
        _getEventAvailabilities = Scope.ServiceProvider.GetRequiredService<GetEventAvailabilities>();
        _updateEventParams = Scope.ServiceProvider.GetRequiredService<UpdateEventParams>();
    }

    private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";
    private const string FriendName = "Bob";
    private const string FriendTimezone = "UTC";

    private static readonly DateTimeOffset Early = Utc(2026, 9, 19, 9, 0);
    private static readonly DateTimeOffset Late = Utc(2026, 9, 19, 14, 0);

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private async Task<(string PublicId, string ParticipantId)> CreateEventAndJoinFriend(
        string friendTimezone = FriendTimezone,
        string? eventTimezone = null,
        int intervalMinutes = 30)
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        if (eventTimezone is not null || intervalMinutes != 30)
        {
            await _updateEventParams.Execute(
                created.PublicId, Guid.Parse(TestEvents.OrganizerGuestId), eventTimezone, intervalMinutes, 9, 21);
        }

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, friendTimezone);

        return (created.PublicId, joined.ParticipantId);
    }

    private static List<(DateTimeOffset Start, DateTimeOffset End)> Range(params (DateTimeOffset Start, DateTimeOffset End)[] ranges)
        => ranges.ToList();

    private Task<SetAvailabilityResult> Save(string publicId, string participantId, params (DateTimeOffset Start, DateTimeOffset End)[] ranges)
        => _setAvailability.Execute(publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), Range(ranges));

    private async Task<Availability> StoredSingle(string participantId)
    {
        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        return await db.Availabilities
            .AsNoTracking()
            .Where(a => a.ParticipantId == ParticipantId.FromString(participantId))
            .SingleAsync();
    }

    [Fact]
    public async Task Execute_ShouldSaveAndReturnRanges()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        var result = await Save(publicId, participantId, (Early, Early.AddHours(1)), (Late, Late.AddHours(1)));

        Assert.Equal(participantId, result.ParticipantId);
        Assert.Equal(FriendName, result.Name);
        Assert.Equal(2, result.Ranges.Count);
        Assert.Equal(Early, result.Ranges[0].Start);
        Assert.Equal(Late, result.Ranges[1].Start);
    }

    [Fact]
    public async Task Execute_ShouldPassAlignedInstantsThroughUnchanged()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend(eventTimezone: "America/New_York");

        var start = Utc(2026, 11, 3, 14, 0);
        var end = Utc(2026, 11, 3, 18, 0);

        var result = await Save(publicId, participantId, (start, end));

        var echoed = Assert.Single(result.Ranges);
        Assert.Equal(start, echoed.Start);
        Assert.Equal(end, echoed.End);
        Assert.Equal(TimeSpan.Zero, echoed.Start.Offset);

        var stored = await StoredSingle(participantId);
        Assert.Equal(start, stored.Start);
        Assert.Equal(end, stored.End);

        var read = Assert.Single(
            (await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId), TestEvents.OrganizerGuid)).Ranges);
        Assert.Equal(start, read.Start);
        Assert.Equal(end, read.End);
    }

    [Fact]
    public async Task Execute_ShouldNormalizeNonUtcOffsetsToUtc()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        var berlin = TimeSpan.FromHours(2);
        var result = await Save(
            publicId,
            participantId,
            (new DateTimeOffset(2026, 9, 19, 9, 0, 0, berlin), new DateTimeOffset(2026, 9, 19, 10, 0, 0, berlin)));

        var range = Assert.Single(result.Ranges);
        Assert.Equal(Utc(2026, 9, 19, 7, 0), range.Start);
        Assert.Equal(Utc(2026, 9, 19, 8, 0), range.End);
        Assert.Equal(TimeSpan.Zero, range.Start.Offset);

        var stored = await StoredSingle(participantId);
        Assert.Equal(Utc(2026, 9, 19, 7, 0), stored.Start);
        Assert.Equal(Utc(2026, 9, 19, 8, 0), stored.End);
    }

    [Fact]
    public async Task Execute_ShouldReplacePriorRanges()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Save(publicId, participantId, (Early, Early.AddHours(1)));
        await Save(publicId, participantId, (Late, Late.AddHours(1)));

        var read = await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId), TestEvents.OrganizerGuid);

        Assert.Single(read.Ranges);
        Assert.Equal(Late, read.Ranges[0].Start);
    }

    [Fact]
    public async Task Execute_ShouldClearSelectionWhenRangesEmpty()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Save(publicId, participantId, (Early, Early.AddHours(1)), (Late, Late.AddHours(1)));
        await Save(publicId, participantId);

        var read = await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId), TestEvents.OrganizerGuid);

        Assert.Empty(read.Ranges);
    }

    [Fact]
    public async Task Execute_ShouldNotChangeParticipantTimezone()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend(
            friendTimezone: "America/Vancouver", eventTimezone: "America/New_York");

        await Save(publicId, participantId, (Utc(2026, 11, 3, 14, 0), Utc(2026, 11, 3, 18, 0)));

        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        db.ChangeTracker.Clear();
        var participant = await db.Participants
            .AsNoTracking()
            .SingleAsync(p => p.ParticipantId == ParticipantId.FromString(participantId));

        Assert.Equal("America/Vancouver", participant.Timezone);
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenGuestIdDoesNotMatch()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<ForbiddenException>(() => _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.NewGuid(), Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _setAvailability.Execute(
            "no-such-public-id", Guid.NewGuid(), Guid.Parse(FriendGuestId), Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownParticipant()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _setAvailability.Execute(
            created.PublicId, Guid.NewGuid(), Guid.Parse(FriendGuestId), Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowForCrossEventParticipant()
    {
        var (_, participantId) = await CreateEventAndJoinFriend();

        var second = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _setAvailability.Execute(
            second.PublicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenEndNotAfterStart()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(
            () => Save(publicId, participantId, (Early, Early)));

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(
            () => Save(publicId, participantId, (Early, Early.AddMinutes(-30))));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenDurationExceeds24Hours()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(
            () => Save(publicId, participantId, (Early, Early.AddHours(24).AddSeconds(1))));
    }

    [Fact]
    public async Task Execute_ShouldSnapMisalignedRangesOutwardAndEcho()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        var result = await Save(publicId, participantId, (Utc(2026, 9, 19, 9, 5), Utc(2026, 9, 19, 9, 50)));

        Assert.Equal(Utc(2026, 9, 19, 9, 0), result.Ranges[0].Start);
        Assert.Equal(Utc(2026, 9, 19, 10, 0), result.Ranges[0].End);

        var stored = await StoredSingle(participantId);
        Assert.Equal(Utc(2026, 9, 19, 9, 0), stored.Start);
        Assert.Equal(Utc(2026, 9, 19, 10, 0), stored.End);
    }

    [Fact]
    public async Task Execute_ShouldSnapInEventTimezoneForHalfHourOffsetViewer()
    {
        // A Kolkata viewer on a New York, 60-min event marks 19:00-20:00 IST
        // (13:30-14:30Z, 09:30-10:30 EDT). It is widened on the New York grid to
        // 09:00-11:00 EDT and survives the read-side projection unchanged.
        var (publicId, participantId) = await CreateEventAndJoinFriend(
            friendTimezone: "Asia/Kolkata", eventTimezone: "America/New_York", intervalMinutes: 60);

        var result = await Save(publicId, participantId, (Utc(2026, 9, 19, 13, 30), Utc(2026, 9, 19, 14, 30)));

        var echoed = Assert.Single(result.Ranges);
        Assert.Equal(Utc(2026, 9, 19, 13, 0), echoed.Start);
        Assert.Equal(Utc(2026, 9, 19, 15, 0), echoed.End);

        var read = Assert.Single(
            (await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId), TestEvents.OrganizerGuid)).Ranges);
        Assert.Equal(Utc(2026, 9, 19, 13, 0), read.Start);
        Assert.Equal(Utc(2026, 9, 19, 15, 0), read.End);

        var eventRead = await _getEventAvailabilities.Execute(publicId, TestEvents.OrganizerGuid);
        var friendRange = Assert.Single(
            eventRead.Participants.Single(p => p.ParticipantId == participantId).Ranges);
        Assert.Equal(read, friendRange);
    }

    [Fact]
    public async Task Execute_ShouldSnapOnUtcGridForLegacyEventWithoutTimezone()
    {
        // Same input on an event without a timezone: the grid is UTC, where
        // 13:30-14:30Z widens to 13:00-15:00Z.
        var (publicId, participantId) = await CreateEventAndJoinFriend(
            friendTimezone: "Asia/Kolkata", intervalMinutes: 60);

        var result = await Save(publicId, participantId, (Utc(2026, 9, 19, 13, 30), Utc(2026, 9, 19, 14, 15)));

        var echoed = Assert.Single(result.Ranges);
        Assert.Equal(Utc(2026, 9, 19, 13, 0), echoed.Start);
        Assert.Equal(Utc(2026, 9, 19, 15, 0), echoed.End);
    }

    [Fact]
    public async Task Execute_ShouldRejectSnappedRangeOver24Hours()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend(intervalMinutes: 60);

        var start = Utc(2026, 9, 19, 9, 1);

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(
            () => Save(publicId, participantId, (start, start.AddHours(24))));

        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        var count = await db.Availabilities
            .CountAsync(a => a.ParticipantId == ParticipantId.FromString(participantId));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Execute_ShouldProjectStoredRangesOntoCurrentIntervalWhenRead()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Save(publicId, participantId, (Utc(2026, 9, 19, 8, 30), Utc(2026, 9, 19, 9, 30)));

        await _updateEventParams.Execute(
            publicId, Guid.Parse(TestEvents.OrganizerGuestId), null, 60, 9, 21);

        var read = await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId), TestEvents.OrganizerGuid);

        Assert.Empty(read.Ranges);

        var stored = await StoredSingle(participantId);
        Assert.Equal(Utc(2026, 9, 19, 8, 30), stored.Start);
        Assert.Equal(Utc(2026, 9, 19, 9, 30), stored.End);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class SetAvailabilityTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private JoinEvent _joinEvent = default!;
    private SetAvailability _setAvailability = default!;
    private GetParticipantAvailability _getParticipantAvailability = default!;

    public SetAvailabilityTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _joinEvent = Scope.ServiceProvider.GetRequiredService<JoinEvent>();
        _setAvailability = Scope.ServiceProvider.GetRequiredService<SetAvailability>();
        _getParticipantAvailability = Scope.ServiceProvider.GetRequiredService<GetParticipantAvailability>();
    }

    private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";
    private const string FriendName = "Bob";
    private const string FriendTimezone = "UTC";

    private static readonly DateTime Early = new(2026, 9, 19, 9, 0, 0);
    private static readonly DateTime Late = new(2026, 9, 19, 14, 0, 0);

    private async Task<(string PublicId, string ParticipantId)> CreateEventAndJoinFriend(string timezone = FriendTimezone)
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName, timezone);

        return (created.PublicId, joined.ParticipantId);
    }

    private static List<AvailabilityRangeResult> Range(params (DateTime Start, DateTime End)[] ranges)
        => ranges.Select(r => new AvailabilityRangeResult(r.Start, r.End)).ToList();

    [Fact]
    public async Task Execute_ShouldSaveAndReturnRanges()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        var result = await _setAvailability.Execute(
            publicId,
            Guid.Parse(participantId),
            Guid.Parse(FriendGuestId),
            FriendTimezone,
            Range((Early, Early.AddHours(1)), (Late, Late.AddHours(1))));

        Assert.Equal(participantId, result.ParticipantId);
        Assert.Equal(FriendName, result.Name);
        Assert.Equal(FriendTimezone, result.Timezone);
        Assert.Equal(2, result.Ranges.Count);
        Assert.Equal(Early, result.Ranges[0].Start);
        Assert.Equal(Late, result.Ranges[1].Start);
    }

    [Fact]
    public async Task Execute_ShouldStoreUtcInstantsAndReturnWallClock()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        var start = new DateTime(2026, 9, 19, 9, 0, 0);
        var end = new DateTime(2026, 9, 19, 10, 0, 0);

        var result = await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), "Europe/Berlin",
            Range((start, end)));

        Assert.Equal(start, result.Ranges[0].Start);
        Assert.Equal(end, result.Ranges[0].End);

        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        var stored = await db.Availabilities
            .Where(a => a.ParticipantId == ParticipantId.FromString(participantId))
            .SingleAsync();

        Assert.Equal(new DateTimeOffset(2026, 9, 19, 7, 0, 0, TimeSpan.Zero), stored.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 8, 0, 0, TimeSpan.Zero), stored.End);
    }

    [Fact]
    public async Task Execute_ShouldReplacePriorRanges()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), FriendTimezone,
            Range((Early, Early.AddHours(1))));

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), FriendTimezone,
            Range((Late, Late.AddHours(1))));

        var read = await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId), null);

        Assert.Single(read.Ranges);
        Assert.Equal(Late, read.Ranges[0].Start);
    }

    [Fact]
    public async Task Execute_ShouldClearSelectionWhenRangesEmpty()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), FriendTimezone,
            Range((Early, Early.AddHours(1)), (Late, Late.AddHours(1))));

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), FriendTimezone,
            Range());

        var read = await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId), null);

        Assert.Empty(read.Ranges);
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenGuestIdDoesNotMatch()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<ForbiddenException>(() => _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.NewGuid(), FriendTimezone, Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownEvent()
    {
        await Assert.ThrowsAsync<EventNotFoundException>(() => _setAvailability.Execute(
            "no-such-public-id", Guid.NewGuid(), Guid.Parse(FriendGuestId), FriendTimezone, Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowForUnknownParticipant()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _setAvailability.Execute(
            created.PublicId, Guid.NewGuid(), Guid.Parse(FriendGuestId), FriendTimezone, Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowForCrossEventParticipant()
    {
        var (firstPublicId, participantId) = await CreateEventAndJoinFriend();

        var second = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _setAvailability.Execute(
            second.PublicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), FriendTimezone, Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenEndNotAfterStart()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(() => _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), FriendTimezone,
            Range((Early, Early))));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenDurationExceeds24Hours()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(() => _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), FriendTimezone,
            Range((Early, Early.AddHours(24).AddSeconds(1)))));
    }
}

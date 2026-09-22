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

    private static readonly DateTimeOffset Early = new(2026, 9, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Late = new(2026, 9, 19, 14, 0, 0, TimeSpan.Zero);

    private async Task<(string PublicId, string ParticipantId)> CreateEventAndJoinFriend()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var joined = await _joinEvent.Execute(
            created.PublicId, null, FriendGuestId, FriendName);

        return (created.PublicId, joined.ParticipantId);
    }

    private static List<AvailabilityRangeResult> Range(params (DateTimeOffset Start, DateTimeOffset End)[] ranges)
        => ranges.Select(r => new AvailabilityRangeResult(r.Start, r.End)).ToList();

    [Fact]
    public async Task Execute_ShouldSaveAndReturnRanges()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        var result = await _setAvailability.Execute(
            publicId,
            Guid.Parse(participantId),
            Guid.Parse(FriendGuestId),
            Range((Early, Early.AddHours(1)), (Late, Late.AddHours(1))));

        Assert.Equal(participantId, result.ParticipantId);
        Assert.Equal(FriendName, result.Name);
        Assert.Equal(2, result.Ranges.Count);
        Assert.Equal(Early, result.Ranges[0].Start);
        Assert.Equal(Late, result.Ranges[1].Start);
    }

    [Fact]
    public async Task Execute_ShouldReplacePriorRanges()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId),
            Range((Early, Early.AddHours(1))));

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId),
            Range((Late, Late.AddHours(1))));

        var read = await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId));

        Assert.Single(read.Ranges);
        Assert.Equal(Late, read.Ranges[0].Start);
    }

    [Fact]
    public async Task Execute_ShouldClearSelectionWhenRangesEmpty()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId),
            Range((Early, Early.AddHours(1)), (Late, Late.AddHours(1))));

        await _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId),
            Range());

        var read = await _getParticipantAvailability.Execute(publicId, Guid.Parse(participantId));

        Assert.Empty(read.Ranges);
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
        var (firstPublicId, participantId) = await CreateEventAndJoinFriend();

        var second = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        await Assert.ThrowsAsync<ParticipantNotFoundException>(() => _setAvailability.Execute(
            second.PublicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), Range()));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenEndNotAfterStart()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(() => _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId),
            Range((Early, Early))));
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenDurationExceeds24Hours()
    {
        var (publicId, participantId) = await CreateEventAndJoinFriend();

        await Assert.ThrowsAsync<InvalidAvailabilityRangeException>(() => _setAvailability.Execute(
            publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId),
            Range((Early, Early.AddHours(24).AddSeconds(1)))));
    }
}

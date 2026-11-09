using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class AdoptLegacyGuestTests : IntegrationTest
{
    private AdoptLegacyGuest _adoptLegacyGuest = default!;
    private CreateEvent _createEvent = default!;
    private ReadGuestService _readGuestService = default!;
    private ReadEventService _readEventService = default!;
    private GuestTokenHasher _hasher = default!;

    public AdoptLegacyGuestTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _adoptLegacyGuest = Scope.ServiceProvider.GetRequiredService<AdoptLegacyGuest>();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _readGuestService = Scope.ServiceProvider.GetRequiredService<ReadGuestService>();
        _readEventService = Scope.ServiceProvider.GetRequiredService<ReadEventService>();
        _hasher = Scope.ServiceProvider.GetRequiredService<GuestTokenHasher>();
    }

    private static readonly Guid LegacyGuestId = Guid.Parse(TestEvents.OrganizerGuestId);

    private async Task<Guid> ResolveGuestId(string token)
    {
        var guest = await _readGuestService.FindByTokenHash(_hasher.Hash(token));
        Assert.NotNull(guest);
        return guest.GuestId;
    }

    [Fact]
    public async Task Execute_ShouldAdoptLegacyIdWithParticipations()
    {
        var created = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        var result = await _adoptLegacyGuest.Execute(LegacyGuestId);

        var guestId = await ResolveGuestId(result.GuestToken);
        Assert.Equal(LegacyGuestId, guestId);

        var events = await _readEventService.SearchEvents(guestId);
        var listed = Assert.Single(events);
        Assert.Equal(created.PublicId, listed.PublicId);
        Assert.True(listed.IsOrganizer);
    }

    [Fact]
    public async Task Execute_ShouldThrowWhenLegacyIdAlreadyClaimed()
    {
        await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);
        await _adoptLegacyGuest.Execute(LegacyGuestId);

        await Assert.ThrowsAsync<GuestAlreadyClaimedException>(
            () => _adoptLegacyGuest.Execute(LegacyGuestId));
    }

    [Fact]
    public async Task Execute_ShouldCreateFreshGuestForUnknownLegacyId()
    {
        var result = await _adoptLegacyGuest.Execute(LegacyGuestId);

        var guestId = await ResolveGuestId(result.GuestToken);
        Assert.NotEqual(LegacyGuestId, guestId);
    }
}

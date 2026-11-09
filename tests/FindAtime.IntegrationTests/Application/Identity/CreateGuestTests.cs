using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class CreateGuestTests : IntegrationTest
{
    private CreateGuest _createGuest = default!;
    private ReadGuestService _readGuestService = default!;
    private GuestTokenHasher _hasher = default!;

    public CreateGuestTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createGuest = Scope.ServiceProvider.GetRequiredService<CreateGuest>();
        _readGuestService = Scope.ServiceProvider.GetRequiredService<ReadGuestService>();
        _hasher = Scope.ServiceProvider.GetRequiredService<GuestTokenHasher>();
    }

    [Fact]
    public async Task Execute_ShouldReturnTokenThatResolvesToGuest()
    {
        var result = await _createGuest.Execute();

        var guest = await _readGuestService.FindByTokenHash(_hasher.Hash(result.GuestToken.Value));

        Assert.NotNull(guest);
    }

    [Fact]
    public async Task Execute_ShouldPersistOnlyTheTokenHash()
    {
        var result = await _createGuest.Execute();

        var db = Scope.ServiceProvider.GetRequiredService<DbContext>();
        var guest = Assert.Single(db.Guests);
        Assert.Equal(_hasher.Hash(result.GuestToken.Value), guest.TokenHash);
    }

    [Fact]
    public async Task Execute_ShouldCreateDistinctGuests()
    {
        var first = await _createGuest.Execute();
        var second = await _createGuest.Execute();

        var firstGuest = await _readGuestService.FindByTokenHash(_hasher.Hash(first.GuestToken.Value));
        var secondGuest = await _readGuestService.FindByTokenHash(_hasher.Hash(second.GuestToken.Value));

        Assert.NotEqual(firstGuest!.GuestId, secondGuest!.GuestId);
    }

    [Fact]
    public async Task FindByTokenHash_ShouldReturnNullForUnknownToken()
    {
        var guest = await _readGuestService.FindByTokenHash(_hasher.Hash("unknown"));

        Assert.Null(guest);
    }
}

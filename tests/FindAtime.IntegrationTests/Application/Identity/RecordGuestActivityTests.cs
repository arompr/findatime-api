using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class RecordGuestActivityTests : IntegrationTest
{
    private RecordGuestActivity _recordGuestActivity = default!;
    private GuestRepository _guestRepository = default!;
    private DbContext _db = default!;

    public RecordGuestActivityTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _recordGuestActivity = Scope.ServiceProvider.GetRequiredService<RecordGuestActivity>();
        _guestRepository = Scope.ServiceProvider.GetRequiredService<GuestRepository>();
        _db = Scope.ServiceProvider.GetRequiredService<DbContext>();
    }

    [Fact]
    public async Task Execute_ShouldPersistLastSeenOnceTheResolutionHasElapsed()
    {
        var staleAt = DateTimeOffset.UtcNow - Guest.LastSeenResolution - TimeSpan.FromMinutes(1);
        var guest = new Guest(GuestId.FromString(Guid.NewGuid().ToString()), [7, 7, 7], staleAt);
        await _guestRepository.Save(guest);

        await _recordGuestActivity.Execute(guest.Id);

        _db.ChangeTracker.Clear();
        var reloaded = await _db.Guests.SingleAsync(g => g.Id == guest.Id);
        Assert.True(reloaded.LastSeenAt > staleAt);
    }

    [Fact]
    public async Task Execute_ShouldIgnoreUnknownGuest()
    {
        await _recordGuestActivity.Execute(GuestId.FromString(Guid.NewGuid().ToString()));

        Assert.Empty(_db.Guests);
    }
}

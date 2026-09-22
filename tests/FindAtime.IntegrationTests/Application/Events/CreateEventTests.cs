using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class CreateEventTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private EventRepository _repository = default!;

    public CreateEventTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
    }

    [Fact]
    public async Task Execute_ShouldPersistEventWithOrganizer()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        var persisted = await _repository.GetByPublicId(response.PublicId);

        Assert.NotNull(persisted);
        Assert.Equal(TestEvents.Name, persisted.Name);
        var organizer = Assert.Single(persisted.Participants);
        Assert.Equal(TestEvents.OrganizerGuestId, organizer.GuestId.Value);
        Assert.Equal(TestEvents.OrganizerName, organizer.Name);
    }

    [Fact]
    public async Task Execute_ShouldReturnPasscodeInResponse()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, true);

        Assert.True(response.IsPasscodeProtected);
        Assert.Equal(6, response.Passcode!.Length);
        Assert.Matches(new Regex("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$"), response.Passcode);
    }

    [Fact]
    public async Task Execute_ShouldReturnNoPasscodeWhenUnprotected()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        Assert.False(response.IsPasscodeProtected);
        Assert.Null(response.Passcode);
    }

    [Fact]
    public async Task Execute_ShouldReturnPublicIdInResponse()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName, false);

        Assert.False(string.IsNullOrEmpty(response.PublicId));
    }
}

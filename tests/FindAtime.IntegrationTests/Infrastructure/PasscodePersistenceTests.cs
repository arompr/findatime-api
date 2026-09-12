using Microsoft.Extensions.DependencyInjection;

[Collection(PostgresCollection.Name)]
public class PasscodePersistenceTests : IntegrationTest
{
    private CreateEvent _createEvent = default!;
    private EventRepository _repository = default!;
    private PasscodeHasher _hasher = default!;

    public PasscodePersistenceTests(PostgresFixture postgres) : base(postgres) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _createEvent = Scope.ServiceProvider.GetRequiredService<CreateEvent>();
        _repository = Scope.ServiceProvider.GetRequiredService<EventRepository>();
        _hasher = Scope.ServiceProvider.GetRequiredService<PasscodeHasher>();
    }

    [Fact]
    public async Task CreatedEvent_ShouldPersistVerifiablePasscodeHash()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var persisted = await _repository.GetById(Guid.Parse(response.EventId));

        Assert.NotNull(persisted);
        Assert.True(_hasher.Verify(response.Passcode, persisted.PasscodeHash));
    }

    [Fact]
    public async Task CreatedEvent_ShouldNotVerifyAgainstWrongPasscode()
    {
        var response = await _createEvent.Execute(
            TestEvents.Name, TestEvents.OrganizerGuestId, TestEvents.OrganizerName);

        var persisted = await _repository.GetById(Guid.Parse(response.EventId));

        Assert.NotNull(persisted);
        Assert.False(_hasher.Verify("ZZZZZZ", persisted.PasscodeHash));
    }
}

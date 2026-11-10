namespace FindAtime.UnitTests.Identity;

public sealed class GuestTokenFactoryTests
{
    private readonly GuestTokenHasher _hasher = new();
    private readonly GuestTokenFactory _factory;

    public GuestTokenFactoryTests()
    {
        _factory = new GuestTokenFactory(_hasher, new GuestTokenGenerator());
    }

    [Fact]
    public void CreateToken_ShouldReturnTheHashOfTheToken()
    {
        var created = _factory.CreateToken();

        Assert.Equal(_hasher.Hash(created.Token.Value), created.Hash);
    }
}

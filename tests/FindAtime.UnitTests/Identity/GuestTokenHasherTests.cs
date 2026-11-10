namespace FindAtime.UnitTests.Identity;

public class GuestTokenHasherTests
{
    private readonly GuestTokenHasher _hasher = new();

    [Fact]
    public void Hash_ShouldReturn32Bytes()
    {
        Assert.Equal(32, _hasher.Hash("token").Value.Length);
    }

    [Fact]
    public void Hash_ShouldBeDeterministic()
    {
        Assert.Equal(_hasher.Hash("token"), _hasher.Hash("token"));
    }

    [Fact]
    public void Hash_ShouldDifferPerToken()
    {
        Assert.NotEqual(_hasher.Hash("token-a"), _hasher.Hash("token-b"));
    }
}

namespace FindAtime.UnitTests.Common;

public class PasscodeHasherTests
{
    private readonly PasscodeHasher _hasher = new();

    [Fact]
    public void Hash_ShouldProduceFixedSizeHashAndSalt()
    {
        var (hash, salt) = _hasher.Hash("ABC234");

        Assert.Equal(PasscodePolicy.HashBytes, hash.Length);
        Assert.Equal(PasscodePolicy.SaltBytes, salt.Length);
    }

    [Fact]
    public void Verify_ShouldReturnTrueForMatchingPasscode()
    {
        var (hash, salt) = _hasher.Hash("ABC234");

        Assert.True(_hasher.Verify("ABC234", hash, salt));
    }

    [Fact]
    public void Verify_ShouldReturnFalseForDifferentPasscode()
    {
        var (hash, salt) = _hasher.Hash("ABC234");

        Assert.False(_hasher.Verify("XYZ789", hash, salt));
    }

    [Fact]
    public void Verify_ShouldReturnFalseForTamperedHash()
    {
        var (hash, salt) = _hasher.Hash("ABC234");

        hash[0] ^= 0xFF;

        Assert.False(_hasher.Verify("ABC234", hash, salt));
    }

    [Fact]
    public void Verify_ShouldReturnFalseForTamperedSalt()
    {
        var (hash, salt) = _hasher.Hash("ABC234");

        salt[0] ^= 0xFF;

        Assert.False(_hasher.Verify("ABC234", hash, salt));
    }

    [Fact]
    public void Hash_ShouldUseDifferentSaltEachTime()
    {
        var (hash1, salt1) = _hasher.Hash("ABC234");
        var (hash2, salt2) = _hasher.Hash("ABC234");

        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);
    }
}

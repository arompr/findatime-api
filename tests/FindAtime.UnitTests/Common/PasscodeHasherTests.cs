namespace FindAtime.UnitTests.Common;

public class PasscodeHasherTests
{
    private readonly PasscodeHasher _hasher = new();

    [Fact]
    public void Hash_ShouldProduceFixedSizeHashAndSalt()
    {
        PasscodeHash passcodeHash = _hasher.Hash("ABC234");

        Assert.Equal(PasscodePolicy.HashBytes, passcodeHash.Hash.Length);
        Assert.Equal(PasscodePolicy.SaltBytes, passcodeHash.Salt.Length);
    }

    [Fact]
    public void Verify_ShouldReturnTrueForMatchingPasscode()
    {
        PasscodeHash passcodeHash = _hasher.Hash("ABC234");

        Assert.True(_hasher.Verify("ABC234", passcodeHash));
    }

    [Fact]
    public void Verify_ShouldReturnFalseForDifferentPasscode()
    {
        PasscodeHash passcodeHash = _hasher.Hash("ABC234");

        Assert.False(_hasher.Verify("XYZ789", passcodeHash));
    }

    [Fact]
    public void Verify_ShouldReturnFalseForTamperedHash()
    {
        PasscodeHash passcodeHash = _hasher.Hash("ABC234");

        passcodeHash.Hash[0] ^= 0xFF;

        Assert.False(_hasher.Verify("ABC234", passcodeHash));
    }

    [Fact]
    public void Verify_ShouldReturnFalseForTamperedSalt()
    {
        PasscodeHash passcodeHash = _hasher.Hash("ABC234");

        passcodeHash.Salt[0] ^= 0xFF;

        Assert.False(_hasher.Verify("ABC234", passcodeHash));
    }

    [Fact]
    public void Hash_ShouldUseDifferentSaltEachTime()
    {
        PasscodeHash passcodeHash1 = _hasher.Hash("ABC234");
        PasscodeHash passcodeHash2 = _hasher.Hash("ABC234");

        Assert.NotEqual(passcodeHash1.Salt, passcodeHash2.Salt);
        Assert.NotEqual(passcodeHash1.Hash, passcodeHash2.Hash);
    }
}

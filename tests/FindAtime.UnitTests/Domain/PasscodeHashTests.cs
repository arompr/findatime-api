namespace FindAtime.UnitTests.Domain;

public sealed class PasscodeHashTests
{
    [Fact]
    public void Equals_ShouldReturnTrue_WhenHashAndSaltHaveSameBytes()
    {
        var hash = new byte[] { 1, 2, 3 };
        var salt = new byte[] { 4, 5, 6 };

        var first = PasscodeHash.FromBytes(hash, salt);
        var second = PasscodeHash.FromBytes(hash.ToArray(), salt.ToArray());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenHashDiffers()
    {
        var first = PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]);

        var second = PasscodeHash.FromBytes([1, 2, 4], [4, 5, 6]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenSaltDiffers()
    {
        var first = PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]);

        var second = PasscodeHash.FromBytes([1, 2, 3], [4, 5, 7]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenHashLengthsDiffer()
    {
        var first = PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]);

        var second = PasscodeHash.FromBytes([1, 2], [4, 5, 6]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenSaltLengthsDiffer()
    {
        var first = PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]);

        var second = PasscodeHash.FromBytes([1, 2, 3], [4, 5]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_WhenHashAndSaltHaveSameBytes()
    {
        var first = PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]);

        var second = PasscodeHash.FromBytes([1, 2, 3], [4, 5, 6]);

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void FromBytes_ShouldCopyInputArrays()
    {
        var hash = new byte[] { 1, 2, 3 };
        var salt = new byte[] { 4, 5, 6 };

        var passcodeHash = PasscodeHash.FromBytes(hash, salt);

        hash[0] = 99;
        salt[0] = 99;

        Assert.Equal([1, 2, 3], passcodeHash.Hash);
        Assert.Equal([4, 5, 6], passcodeHash.Salt);
    }
}

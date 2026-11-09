namespace FindAtime.UnitTests.Identity;

public sealed class GuestTokenHashTests
{
    [Fact]
    public void Equals_ShouldReturnTrue_WhenBytesAreTheSame()
    {
        var bytes = new byte[] { 1, 2, 3 };

        Assert.Equal(GuestTokenHash.FromBytes(bytes), GuestTokenHash.FromBytes(bytes.ToArray()));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenBytesDiffer()
    {
        Assert.NotEqual(GuestTokenHash.FromBytes([1, 2, 3]), GuestTokenHash.FromBytes([1, 2, 4]));
    }

    [Fact]
    public void FromBytes_ShouldCopyTheInput()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var hash = GuestTokenHash.FromBytes(bytes);

        bytes[0] = 9;

        Assert.Equal(1, hash.Value[0]);
    }
}

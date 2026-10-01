public sealed record PasscodeHash
{
    public byte[] Hash { get; }
    public byte[] Salt { get; }

    private PasscodeHash(byte[] hash, byte[] salt)
    {
        Hash = hash;
        Salt = salt;
    }

    public static PasscodeHash FromBytes(byte[] hash, byte[] salt)
    {
        return new PasscodeHash(hash.ToArray(), salt.ToArray());
    }

    public bool Equals(PasscodeHash? other)
    {
        return other is not null && Hash.AsSpan().SequenceEqual(other.Hash) && Salt.AsSpan().SequenceEqual(other.Salt);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        foreach (var b in Hash)
            hashCode.Add(b);

        foreach (var b in Salt)
            hashCode.Add(b);

        return hashCode.ToHashCode();
    }
}

public sealed record GuestTokenHash
{
    public byte[] Value { get; }

    private GuestTokenHash(byte[] value)
    {
        Value = value;
    }

    public static GuestTokenHash FromBytes(byte[] value)
    {
        return new GuestTokenHash(value.ToArray());
    }

    public bool Equals(GuestTokenHash? other)
    {
        return other is not null && Value.AsSpan().SequenceEqual(other.Value);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.AddBytes(Value);
        return hashCode.ToHashCode();
    }
}

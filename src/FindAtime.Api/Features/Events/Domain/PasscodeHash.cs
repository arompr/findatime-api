public readonly record struct PasscodeHash
{
    public byte[] Hash { get; }
    public byte[] Salt { get; }

    private PasscodeHash(byte[] hash, byte[] salt)
    {
        this.Hash = hash;
        this.Salt = salt;
    }

    public static PasscodeHash FromBytes(byte[] hash, byte[] salt)
    {
        return new PasscodeHash(hash, salt);
    }
}

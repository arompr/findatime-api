public sealed record PasscodeCreated
{
    public Passcode Passcode { get; }
    public PasscodeHash Hash { get; }

    private PasscodeCreated(Passcode passcode, PasscodeHash hash)
    {
        this.Passcode = passcode;
        this.Hash = hash;
    }

    public static PasscodeCreated Create(Passcode passcode, PasscodeHash hash)
    {
        return new PasscodeCreated(passcode, hash);
    }

    public static PasscodeCreated FromString(string value, PasscodeHash hash)
    {
        return new PasscodeCreated(Passcode.FromString(value), hash);
    }
}

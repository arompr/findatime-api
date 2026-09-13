public readonly record struct Passcode
{
    public string Value { get; }

    private Passcode(string value)
    {
        this.Value = value;
    }

    public static Passcode FromString(string value)
    {
        return new Passcode(value);
    }
}

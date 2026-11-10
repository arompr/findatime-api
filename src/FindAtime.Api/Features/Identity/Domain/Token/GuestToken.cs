public readonly record struct GuestToken
{
    public string Value { get; }

    private GuestToken(string value)
    {
        this.Value = value;
    }

    public static GuestToken FromString(string value)
    {
        return new GuestToken(value);
    }
}

public readonly record struct GuestId
{
    public string Value { get; }

    private GuestId(string value)
    {
        this.Value = value;
    }

    public static GuestId FromString(string value)
    {
        return new GuestId(value);
    }
}
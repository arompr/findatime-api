public readonly record struct EventId
{
    public string Value { get; }

    private EventId(string value)
    {
        this.Value = value;
    }

    public static EventId FromString(string value)
    {
        return new EventId(value);
    }
}
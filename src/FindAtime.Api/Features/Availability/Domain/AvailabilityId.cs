public readonly record struct AvailabilityId
{
    public string Value { get; }

    private AvailabilityId(string value)
    {
        this.Value = value;
    }

    public static AvailabilityId FromString(string value)
    {
        return new AvailabilityId(value);
    }
}

public readonly record struct ParticipantUuid
{
    public string Value { get; }

    private ParticipantUuid(string value)
    {
        this.Value = value;
    }

    public static ParticipantUuid FromString(string value)
    {
        return new ParticipantUuid(value);
    }
}
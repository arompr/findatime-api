public readonly record struct ParticipantId
{
    public string Value { get; }

    private ParticipantId(string value)
    {
        this.Value = value;
    }

    public static ParticipantId FromString(string value)
    {
        return new ParticipantId(value);
    }
}
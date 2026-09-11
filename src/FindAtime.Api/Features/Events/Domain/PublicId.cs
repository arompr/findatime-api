public readonly record struct PublicId
{
    public string Value { get; }

    private PublicId(string value)
    {
        this.Value = value;
    }

    public static PublicId FromString(string value)
    {
        return new PublicId(value);
    }
}
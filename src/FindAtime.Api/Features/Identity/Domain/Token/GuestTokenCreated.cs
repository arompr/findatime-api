public sealed record GuestTokenCreated
{
    public GuestToken Token { get; }
    public GuestTokenHash Hash { get; }

    private GuestTokenCreated(GuestToken token, GuestTokenHash hash)
    {
        this.Token = token;
        this.Hash = hash;
    }

    public static GuestTokenCreated Create(GuestToken token, GuestTokenHash hash)
    {
        return new GuestTokenCreated(token, hash);
    }
}

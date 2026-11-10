public class GuestTokenFactory
{
    private GuestTokenHasher _tokenHasher;
    private GuestTokenGenerator _tokenGenerator;

    public GuestTokenFactory(GuestTokenHasher tokenHasher, GuestTokenGenerator tokenGenerator)
    {
        this._tokenHasher = tokenHasher;
        this._tokenGenerator = tokenGenerator;
    }

    public GuestTokenCreated CreateToken()
    {
        GuestToken token = this._tokenGenerator.Generate();
        GuestTokenHash tokenHash = this._tokenHasher.Hash(token.Value);
        return GuestTokenCreated.Create(token, tokenHash);
    }
}

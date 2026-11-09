using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

public class GuestTokenGenerator
{
    public GuestToken Generate()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(GuestTokenPolicy.TokenBytes);
        return GuestToken.FromString(WebEncoders.Base64UrlEncode(bytes));
    }
}

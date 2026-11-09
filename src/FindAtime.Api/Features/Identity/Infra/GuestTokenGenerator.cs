using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

public class GuestTokenGenerator
{
    public const int TokenBytes = 32;

    public string Generate()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
    }
}

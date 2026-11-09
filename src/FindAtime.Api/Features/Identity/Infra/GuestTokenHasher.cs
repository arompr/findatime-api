using System.Security.Cryptography;
using System.Text;

public class GuestTokenHasher
{
    public GuestTokenHash Hash(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return GuestTokenHash.FromBytes(hash);
    }
}

using System.Security.Cryptography;
using System.Text;

public class GuestTokenHasher
{
    public byte[] Hash(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}

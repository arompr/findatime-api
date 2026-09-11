using System.Security.Cryptography;

public class PasscodeHasher
{
    public (byte[] Hash, byte[] Salt) Hash(string passcode)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(PasscodePolicy.SaltBytes);
        byte[] hash = ComputeHash(passcode, salt);
        return (hash, salt);
    }

    public bool Verify(string passcode, byte[] hash, byte[] salt)
    {
        byte[] candidate = ComputeHash(passcode, salt);
        return CryptographicOperations.FixedTimeEquals(candidate, hash);
    }

    private static byte[] ComputeHash(string passcode, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            passcode,
            salt,
            PasscodePolicy.Iterations,
            HashAlgorithmName.SHA256,
            PasscodePolicy.HashBytes);
    }
}

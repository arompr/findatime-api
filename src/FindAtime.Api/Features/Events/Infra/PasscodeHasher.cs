using System.Security.Cryptography;

public class PasscodeHasher
{
    public PasscodeHash Hash(string passcode)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(PasscodePolicy.SaltBytes);
        byte[] hash = ComputeHash(passcode, salt);
        return PasscodeHash.FromBytes(hash, salt);
    }

    public bool Verify(string passcode, PasscodeHash passcodeHash)
    {
        byte[] candidate = ComputeHash(passcode, passcodeHash.Salt);
        return CryptographicOperations.FixedTimeEquals(candidate, passcodeHash.Hash);
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

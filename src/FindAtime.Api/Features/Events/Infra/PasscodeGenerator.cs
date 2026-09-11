using System.Security.Cryptography;

public class PasscodeGenerator
{
    public Passcode Generate()
    {
        char[] chars = new char[PasscodePolicy.Length];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = PasscodePolicy.Alphabet[RandomNumberGenerator.GetInt32(PasscodePolicy.Alphabet.Length)];
        }

        return Passcode.FromString(new string(chars));
    }
}

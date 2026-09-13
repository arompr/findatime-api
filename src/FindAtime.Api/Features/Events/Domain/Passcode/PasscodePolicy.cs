public static class PasscodePolicy
{
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 6;
    public const int Iterations = 100_000;
    public const int SaltBytes = 16;
    public const int HashBytes = 32;
}

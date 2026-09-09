using NanoidDotNet;

public class PublicIdGenerator
{
    private const int Length = 12;
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public PublicId Generate()
    {
        return PublicId.FromString(Nanoid.Generate(Alphabet, Length));
    }
}
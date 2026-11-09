using System.Text.RegularExpressions;

namespace FindAtime.UnitTests.Identity;

public class GuestTokenGeneratorTests
{
    private readonly GuestTokenGenerator _generator = new();

    [Fact]
    public void Generate_ShouldReturnBase64UrlTokenOf32Bytes()
    {
        var token = _generator.Generate();

        Assert.Equal(43, token.Length);
        Assert.Matches(new Regex("^[A-Za-z0-9_-]+$"), token);
    }

    [Fact]
    public void Generate_ShouldReturnUniqueTokens()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => _generator.Generate()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }
}

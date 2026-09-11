using System.Text.RegularExpressions;

namespace FindAtime.UnitTests.Common;

public class PasscodeGeneratorTests
{
    private readonly PasscodeGenerator _generator = new();

    [Fact]
    public void Generate_ShouldReturnPasscodeWithValue()
    {
        var passcode = _generator.Generate();

        Assert.False(string.IsNullOrEmpty(passcode.Value));
    }

    [Fact]
    public void Generate_ShouldReturnSixCharacterPasscode()
    {
        var passcode = _generator.Generate();

        Assert.Equal(6, passcode.Value.Length);
    }

    [Fact]
    public void Generate_ShouldReturnUniquePasscodes()
    {
        var passcodes = Enumerable.Range(0, 1000).Select(_ => _generator.Generate().Value).ToList();

        Assert.Equal(passcodes.Count, passcodes.Distinct().Count());
    }

    [Fact]
    public void Generate_ShouldUseOnlyCrockfordAlphabet()
    {
        var passcode = _generator.Generate();

        Assert.Matches(new Regex("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$"), passcode.Value);
    }
}

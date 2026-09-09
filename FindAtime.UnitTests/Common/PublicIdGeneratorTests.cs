using System.Text.RegularExpressions;

namespace FindAtime.UnitTests.Common;

public class PublicIdGeneratorTests
{
    private readonly PublicIdGenerator _generator = new();

    [Fact]
    public void Generate_ShouldReturnPublicIdWithValue()
    {
        var publicId = _generator.Generate();

        Assert.NotNull(publicId);
        Assert.False(string.IsNullOrEmpty(publicId.Value));
    }

    [Fact]
    public void Generate_ShouldReturn12CharacterId()
    {
        var publicId = _generator.Generate();

        Assert.Equal(12, publicId.Value.Length);
    }

    [Fact]
    public void Generate_ShouldReturnUniqueIds()
    {
        var id1 = _generator.Generate();
        var id2 = _generator.Generate();

        Assert.NotEqual(id1.Value, id2.Value);
    }

    [Fact]
    public void Generate_ShouldReturnAlphanumericId()
    {
        var publicId = _generator.Generate();

        Assert.Matches(new Regex("^[a-zA-Z0-9]{12}$"), publicId.Value);
    }
}
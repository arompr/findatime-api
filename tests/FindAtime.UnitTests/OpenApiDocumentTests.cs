using System.Text.Json;

namespace FindAtime.UnitTests;

public class OpenApiDocumentTests : IDisposable
{
    private readonly JsonDocument _document;

    public OpenApiDocumentTests()
    {
        var documentPath = LocateOpenApiDocument();
        var json = File.ReadAllText(documentPath);
        _document = JsonDocument.Parse(json);
    }

    [Fact]
    public void OpenApiVersion_ShouldStartWith32()
    {
        var openApi = _document.RootElement.GetProperty("openapi").GetString();

        Assert.NotNull(openApi);
        Assert.StartsWith("3.2", openApi);
    }

    [Theory]
    [InlineData("/events/{publicId}/availabilities")]
    [InlineData("/events/{publicId}/participants/{participantId}/availability")]
    public void AvailabilityReads_ShouldBeGetOperationsWithoutQuery(string path)
    {
        var item = PathItem(path);

        Assert.True(item.TryGetProperty("get", out var get));
        Assert.False(get.TryGetProperty("requestBody", out _));
        Assert.False(item.TryGetProperty("query", out _));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    public void AvailabilityRangeResponse_ShouldUseDateTimeFormat(string property)
    {
        var format = Schema("AvailabilityRangeResponse")
            .GetProperty("properties")
            .GetProperty(property)
            .GetProperty("format")
            .GetString();

        Assert.Equal("date-time", format);
    }

    [Theory]
    [InlineData("GetEventAvailabilitiesResponse")]
    [InlineData("ParticipantAvailabilityResponse")]
    [InlineData("GetParticipantAvailabilityResponse")]
    [InlineData("SetAvailabilityRequest")]
    [InlineData("SetAvailabilityResponse")]
    public void AvailabilitySchemas_ShouldNotHaveTimezone(string schema)
    {
        Assert.False(Schema(schema).GetProperty("properties").TryGetProperty("timezone", out _));
    }

    private JsonElement PathItem(string path) =>
        _document.RootElement.GetProperty("paths").GetProperty(path);

    private JsonElement Schema(string name) =>
        _document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(name);

    public void Dispose()
    {
        _document.Dispose();
    }

    private static string LocateOpenApiDocument()
    {
        const string relativePath = "src/FindAtime.Api/obj/FindAtime.Api.json";

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var level = 0; level < 8; level++)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
            if (directory == null)
            {
                break;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate '{relativePath}' within 8 levels of '{AppContext.BaseDirectory}'.");
    }
}

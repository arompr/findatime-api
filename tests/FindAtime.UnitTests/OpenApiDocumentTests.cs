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

    [Fact]
    public void EventAvailabilities_ShouldHaveQueryOperation()
    {
        var hasQuery = _document.RootElement
            .GetProperty("paths")
            .GetProperty("/events/{publicId}/availabilities")
            .TryGetProperty("query", out _);

        Assert.True(hasQuery);
    }

    [Fact]
    public void ParticipantAvailability_ShouldHaveQueryOperation()
    {
        var hasQuery = _document.RootElement
            .GetProperty("paths")
            .GetProperty("/events/{publicId}/participants/{participantId}/availability")
            .TryGetProperty("query", out _);

        Assert.True(hasQuery);
    }

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

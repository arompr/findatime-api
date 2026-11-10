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

    [Fact]
    public void GuestSecurityScheme_ShouldBeAnAuthorizationHeaderApiKey()
    {
        var scheme = _document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Guest");

        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("Authorization", scheme.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("/events", "post")]
    [InlineData("/events", "get")]
    [InlineData("/events/{publicId}", "delete")]
    [InlineData("/events/{publicId}/params", "put")]
    [InlineData("/events/{publicId}/join", "post")]
    [InlineData("/events/{publicId}/leave", "post")]
    [InlineData("/events/{publicId}/participant", "get")]
    [InlineData("/events/{publicId}/availabilities", "get")]
    [InlineData("/events/{publicId}/participants/{participantId}/availability", "get")]
    [InlineData("/events/{publicId}/participants/{participantId}/availability", "put")]
    public void AuthenticatedOperations_ShouldRequireGuestScheme(string path, string method)
    {
        var operation = PathItem(path).GetProperty(method);

        var security = operation.GetProperty("security");
        Assert.Contains(security.EnumerateArray(), requirement => requirement.TryGetProperty("Guest", out _));
    }

    [Theory]
    [InlineData("/events/{publicId}", "get")]
    [InlineData("/guests", "post")]
    public void PublicOperations_ShouldNotRequireAuthentication(string path, string method)
    {
        var operation = PathItem(path).GetProperty(method);

        Assert.False(operation.TryGetProperty("security", out _));
    }

    [Theory]
    [InlineData("CreateEventRequestParams")]
    [InlineData("JoinEventRequestParams")]
    public void RequestSchemas_ShouldNotCarryGuestId(string schema)
    {
        Assert.False(Schema(schema).GetProperty("properties").TryGetProperty("guestId", out _));
    }

    [Fact]
    public void SearchEvents_ShouldNotTakeGuestIdQuery()
    {
        var operation = PathItem("/events").GetProperty("get");

        bool hasGuestIdParameter = operation.TryGetProperty("parameters", out var parameters)
            && parameters.EnumerateArray().Any(p => p.GetProperty("name").GetString() == "guestId");
        Assert.False(hasGuestIdParameter);
    }

    [Fact]
    public void CreateGuestResponse_ShouldOnlyExposeTheToken()
    {
        var properties = Schema("CreateGuestResponse").GetProperty("properties")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(["guestToken"], properties);
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

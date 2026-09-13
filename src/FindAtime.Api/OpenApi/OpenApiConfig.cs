using Microsoft.AspNetCore.OpenApi;

public static class OpenApiConfig
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((doc, ctx, ct) =>
        {
            doc.Info.Title = "Findatime API";
            doc.Info.Version = "v1";
            doc.Info.Description = "REST API for scheduling events and meetings.";
            return Task.CompletedTask;
        });
    }
}

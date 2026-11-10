using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

public static class OpenApiConfig
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((doc, ctx, ct) =>
        {
            doc.Info.Title = "Findatime API";
            doc.Info.Version = "v1";
            doc.Info.Description = "REST API for scheduling events and meetings.";

            doc.Components ??= new OpenApiComponents();
            doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            doc.Components.SecuritySchemes[GuestAuthenticationHandler.SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Guest token issued by POST /guests, sent as `Guest <token>`.",
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, ctx, ct) =>
        {
            bool requiresAuth = ctx.Description.ActionDescriptor.EndpointMetadata
                .OfType<IAuthorizeData>()
                .Any();
            if (!requiresAuth)
                return Task.CompletedTask;

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(GuestAuthenticationHandler.SchemeName, ctx.Document)] = [],
            });
            return Task.CompletedTask;
        });
    }
}

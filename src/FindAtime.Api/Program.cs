using System.Threading.RateLimiting;
using DotNetEnv.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(OpenApiConfig.Configure);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Configuration.AddDotNetEnvMulti([".env", $".env.{builder.Environment.EnvironmentName.ToLowerInvariant()}"]);

var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
    });
});

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string is not configured.");

builder.Services.AddFindAtimeServices(connectionString);

builder.Services
    .AddAuthentication(GuestAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, GuestAuthenticationHandler>(
        GuestAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

int guestsPerMinute = builder.Configuration.GetValue("RateLimiting:GuestsPerMinute", 10);
int joinsPerMinute = builder.Configuration.GetValue("RateLimiting:JoinsPerMinute", 10);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many requests. Try again in a minute.", error = "rate_limited" },
            cancellationToken);
    };
    options.AddPolicy(IdentityRestService.GuestsRateLimitPolicy, context => PerIpPerMinute(context, guestsPerMinute));
    options.AddPolicy(EventsRestService.JoinRateLimitPolicy, context => PerIpPerMinute(context, joinsPerMinute));
});

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation(
    "CORS allowed origins (count: {Count}): [{Origins}]",
    allowedOrigins.Length,
    string.Join(", ", allowedOrigins));

if (allowedOrigins.Length == 0)
{
    startupLogger.LogWarning(
        "CORS allowed origins is empty. Cross-origin requests will be blocked.");
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapIdentity();
app.MapEvents();
app.MapAvailability();
app.MapHealth();

app.Run();

static RateLimitPartition<string> PerIpPerMinute(HttpContext context, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

public partial class Program { }

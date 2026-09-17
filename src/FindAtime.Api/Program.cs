using DotNetEnv.Configuration;

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.MapOpenApi();
}

app.UseCors();

app.MapEvents();
app.MapHealth();

app.Run();

public partial class Program { }

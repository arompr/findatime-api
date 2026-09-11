using DotNetEnv.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Configuration.AddDotNetEnvMulti([".env", $".env.{builder.Environment.EnvironmentName.ToLowerInvariant()}"]);

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string is not configured.");

builder.Services.AddFindAtimeServices(connectionString);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapEvents();
app.MapHealth();

app.Run();

public partial class Program { }

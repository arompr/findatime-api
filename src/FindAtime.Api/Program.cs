using DotNetEnv.Configuration;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Configuration.AddDotNetEnvMulti([".env", $".env.{builder.Environment.EnvironmentName.ToLowerInvariant()}"]);

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string is not configured.");

var dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<DbContext>(options => options.UseNpgsql(dataSource));
builder.Services.AddScoped<EventRepository>();
builder.Services.AddScoped<ReadEventService>();
builder.Services.AddSingleton<EventFactory>();
builder.Services.AddSingleton<ParticipantFactory>();
builder.Services.AddSingleton<PublicIdGenerator>();
builder.Services.AddScoped<CreateEvent>();
builder.Services.AddScoped<GetEvent>();

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

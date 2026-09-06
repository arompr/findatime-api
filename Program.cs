using DotNetEnv.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Configuration.AddDotNetEnvMulti([".env", $".env.{builder.Environment.EnvironmentName.ToLowerInvariant()}"]);

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string is not configured.");

builder.Services.AddDbContext<DbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<ReadDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<EventRepository>();
builder.Services.AddScoped<ReadEventService>();
builder.Services.AddSingleton<EventFactory>();
builder.Services.AddSingleton<ParticipantFactory>();
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

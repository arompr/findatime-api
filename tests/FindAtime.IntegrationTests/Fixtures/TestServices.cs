using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public static class TestServices
{
    public static ServiceProvider Build(string connectionString)
    {
        var services = new ServiceCollection();

        var dataSource = NpgsqlDataSource.Create(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<DbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<EventRepository>();
        services.AddScoped<ReadEventService>();
        services.AddSingleton<EventFactory>();
        services.AddSingleton<ParticipantFactory>();
        services.AddSingleton<PublicIdGenerator>();
        services.AddScoped<CreateEvent>();
        services.AddScoped<GetEvent>();

        return services.BuildServiceProvider();
    }
}

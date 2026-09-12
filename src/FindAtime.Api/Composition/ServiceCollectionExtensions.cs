using Microsoft.EntityFrameworkCore;
using Npgsql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFindAtimeServices(
        this IServiceCollection services,
        string connectionString)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);

        services.AddSingleton(dataSource);
        services.AddScoped<IReadConnectionProvider, ReadConnectionProvider>();
        services.AddDbContext<DbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<EventRepository>();
        services.AddScoped<ReadEventService>();
        services.AddSingleton<EventFactory>();
        services.AddSingleton<ParticipantFactory>();
        services.AddSingleton<PublicIdGenerator>();
        services.AddSingleton<PasscodeGenerator>();
        services.AddSingleton<PasscodeHasher>();
        services.AddScoped<CreateEvent>();
        services.AddScoped<GetEvent>();
        services.AddScoped<GetEventByPublicId>();
        services.AddScoped<JoinEvent>();
        services.AddScoped<LeaveEvent>();
        services.AddScoped<SearchEvents>();

        return services;
    }
}

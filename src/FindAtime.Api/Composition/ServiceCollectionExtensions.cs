using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddScoped<ReadAvailabilityService>();
        services.AddSingleton<AvailabilityFactory>();
        services.AddScoped<AvailabilityRepository>();
        services.AddScoped<GetMyParticipant>();
        services.AddScoped<GetEventAvailabilities>();
        services.AddScoped<GetParticipantAvailability>();
        services.AddScoped<SetAvailability>();
        services.AddSingleton<EventFactory>();
        services.AddSingleton<ParticipantFactory>();
        services.AddSingleton<PublicIdGenerator>();
        services.AddSingleton<PasscodeGenerator>();
        services.AddSingleton<PasscodeHasher>();
        services.AddSingleton<PasscodeFactory>();
        services.AddSingleton<TimezoneConverter>();
        services.AddSingleton<AvailabilityProjection>();
        services.AddScoped<CreateEvent>();
        services.AddScoped<JoinEvent>();
        services.AddScoped<LeaveEvent>();
        services.AddScoped<UpdateEventParams>();
        services.AddScoped<DeleteEvent>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<GuestTokenGenerator>();
        services.AddSingleton<GuestTokenHasher>();
        services.AddScoped<GuestRepository>();
        services.AddScoped<ReadGuestService>();
        services.AddScoped<CreateGuest>();
        services.AddScoped<AdoptLegacyGuest>();

        return services;
    }
}

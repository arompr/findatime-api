public static class HealthRestService
{
    public static void MapHealth(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok()).WithName("Health");
    }
}
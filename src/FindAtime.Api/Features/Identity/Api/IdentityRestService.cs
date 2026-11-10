public static class IdentityRestService
{
    public const string GuestsRateLimitPolicy = "guests";

    public static void MapIdentity(this WebApplication app)
    {
        app.MapPost(
                "/guests",
                async (
                    CreateGuestRequestParams? requestParams,
                    IConfiguration configuration,
                    CreateGuest createGuest,
                    AdoptLegacyGuest adoptLegacyGuest
                ) =>
                {
                    string? legacyGuestId = requestParams?.LegacyGuestId;
                    bool allowLegacyAdoption = configuration.GetValue("Identity:AllowLegacyAdoption", true);

                    CreateGuestResult result;
                    if (!string.IsNullOrWhiteSpace(legacyGuestId) && allowLegacyAdoption)
                    {
                        if (!Guid.TryParse(legacyGuestId, out var parsedLegacyGuestId))
                            return Results.BadRequest("legacyGuestId must be a valid uuid");

                        result = await adoptLegacyGuest.Execute(parsedLegacyGuestId);
                    }
                    else
                    {
                        result = await createGuest.Execute();
                    }

                    return Results.Json(new CreateGuestResponse(result.GuestToken.Value), statusCode: StatusCodes.Status201Created);
                }
            )
            .WithName("CreateGuest")
            .RequireRateLimiting(GuestsRateLimitPolicy)
            .Produces<CreateGuestResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);
    }
}

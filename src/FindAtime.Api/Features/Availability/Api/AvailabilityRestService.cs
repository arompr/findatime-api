using System.Globalization;
using System.Security.Claims;

public static class AvailabilityRestService
{
    public static void MapAvailability(this WebApplication app)
    {
        app.MapGet(
                "/events/{publicId}/participant",
                async (string publicId, ClaimsPrincipal user, GetMyParticipant getMyParticipant) =>
                {
                    GetMyParticipantResult result = await getMyParticipant.Execute(publicId, user.GetGuestId());

                    return Results.Ok(new GetMyParticipantResponse(result.ParticipantId, result.Name));
                }
            )
            .WithName("GetMyParticipant")
            .RequireAuthorization()
            .Produces<GetMyParticipantResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
                "/events/{publicId}/availabilities",
                async (string publicId, ClaimsPrincipal user, GetEventAvailabilities getEventAvailabilities) =>
                {
                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    GetEventAvailabilitiesResult result = await getEventAvailabilities.Execute(publicId, user.GetGuestId());

                    return Results.Ok(new GetEventAvailabilitiesResponse(
                        result.Participants
                            .Select(p => new ParticipantAvailabilityResponse(
                                p.ParticipantId,
                                p.Name,
                                p.Ranges
                                    .Select(r => new AvailabilityRangeResponse(r.Start, r.End))
                                    .ToList()
                            ))
                            .ToList()
                    ));
                }
            )
            .WithName("GetEventAvailabilities")
            .RequireAuthorization()
            .Produces<GetEventAvailabilitiesResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
                "/events/{publicId}/participants/{participantId}/availability",
                async (string publicId, string participantId, ClaimsPrincipal user, GetParticipantAvailability getParticipantAvailability) =>
                {
                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    if (!Guid.TryParse(participantId, out var parsedParticipantId))
                        return Results.BadRequest("participantId must be a valid uuid");

                    ParticipantAvailabilityResult result = await getParticipantAvailability.Execute(publicId, parsedParticipantId, user.GetGuestId());

                    return Results.Ok(new GetParticipantAvailabilityResponse(
                        result.ParticipantId,
                        result.Name,
                        result.Ranges
                            .Select(r => new AvailabilityRangeResponse(r.Start, r.End))
                            .ToList()
                    ));
                }
            )
            .WithName("GetParticipantAvailability")
            .RequireAuthorization()
            .Produces<GetParticipantAvailabilityResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut(
                "/events/{publicId}/participants/{participantId}/availability",
                async (
                    string publicId,
                    string participantId,
                    SetAvailabilityRequest request,
                    ClaimsPrincipal user,
                    SetAvailability setAvailability
                ) =>
                {
                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    if (!Guid.TryParse(participantId, out var parsedParticipantId))
                        return Results.BadRequest("participantId must be a valid uuid");

                    IReadOnlyList<AvailabilityRangeRequest> ranges = request.Ranges ?? [];

                    SetAvailabilityResult result = await setAvailability.Execute(
                        publicId,
                        parsedParticipantId,
                        user.GetGuestId(),
                        ranges
                            .Select(r => (Start: ParseInstant(r.Start), End: ParseInstant(r.End)))
                            .ToList()
                    );

                    return Results.Ok(new SetAvailabilityResponse(
                        result.ParticipantId,
                        result.Name,
                        result.Ranges
                            .Select(r => new AvailabilityRangeResponse(r.Start, r.End))
                            .ToList()
                    ));
                }
            )
            .WithName("SetAvailability")
            .RequireAuthorization()
            .Produces<SetAvailabilityResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    public static DateTimeOffset ParseInstant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var kindProbe)
            || kindProbe.Kind == DateTimeKind.Unspecified
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new InvalidAvailabilityRangeException(
                $"Availability range '{value}' must be an ISO 8601 instant with a UTC offset.");
        }

        return parsed.ToUniversalTime();
    }
}

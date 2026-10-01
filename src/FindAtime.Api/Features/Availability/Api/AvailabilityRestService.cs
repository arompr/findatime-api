using System.Globalization;
using Microsoft.AspNetCore.Mvc;

public static class AvailabilityRestService
{
    public static void MapAvailability(this WebApplication app)
    {
        app.MapGet(
                "/events/{publicId}/participant",
                async (string publicId, HttpContext httpContext, GetMyParticipant getMyParticipant) =>
                {
                    string? guestId = httpContext.Request.Headers["X-Guest-Id"].FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(guestId))
                        return Results.BadRequest("X-Guest-Id header is required");

                    if (!Guid.TryParse(guestId, out var parsedGuestId))
                        return Results.BadRequest("X-Guest-Id header must be a valid uuid");

                    GetMyParticipantResult result = await getMyParticipant.Execute(publicId, parsedGuestId);

                    return Results.Ok(new GetMyParticipantResponse(result.ParticipantId, result.Name));
                }
            )
            .WithName("GetMyParticipant")
            .Produces<GetMyParticipantResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapMethods(
                "/events/{publicId}/availabilities",
                [HttpMethods.Query],
                async (string publicId, [FromBody] AvailabilityQueryRequest request, GetEventAvailabilities getEventAvailabilities, TimezoneConverter timezoneConverter) =>
                {
                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    if (request.Timezone is not null)
                        timezoneConverter.Resolve(request.Timezone);

                    GetEventAvailabilitiesResult result = await getEventAvailabilities.Execute(publicId, request.Timezone);

                    return Results.Ok(new GetEventAvailabilitiesResponse(
                        result.Timezone,
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
            .Produces<GetEventAvailabilitiesResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapMethods(
                "/events/{publicId}/participants/{participantId}/availability",
                [HttpMethods.Query],
                async (string publicId, string participantId, [FromBody] AvailabilityQueryRequest request, GetParticipantAvailability getParticipantAvailability, TimezoneConverter timezoneConverter) =>
                {
                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    if (!Guid.TryParse(participantId, out var parsedParticipantId))
                        return Results.BadRequest("participantId must be a valid uuid");

                    if (request.Timezone is not null)
                        timezoneConverter.Resolve(request.Timezone);

                    ParticipantAvailabilityResult result = await getParticipantAvailability.Execute(publicId, parsedParticipantId, request.Timezone);

                    return Results.Ok(new GetParticipantAvailabilityResponse(
                        result.ParticipantId,
                        result.Name,
                        result.Timezone,
                        result.Ranges
                            .Select(r => new AvailabilityRangeResponse(r.Start, r.End))
                            .ToList()
                    ));
                }
            )
            .WithName("GetParticipantAvailability")
            .Produces<GetParticipantAvailabilityResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut(
                "/events/{publicId}/participants/{participantId}/availability",
                async (
                    string publicId,
                    string participantId,
                    SetAvailabilityRequest request,
                    HttpContext httpContext,
                    SetAvailability setAvailability
                ) =>
                {
                    string? guestId = httpContext.Request.Headers["X-Guest-Id"].FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(guestId))
                        return Results.BadRequest("X-Guest-Id header is required");

                    if (!Guid.TryParse(guestId, out var parsedGuestId))
                        return Results.BadRequest("X-Guest-Id header must be a valid uuid");

                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    if (!Guid.TryParse(participantId, out var parsedParticipantId))
                        return Results.BadRequest("participantId must be a valid uuid");

                    if (string.IsNullOrWhiteSpace(request.Timezone))
                        return Results.BadRequest("timezone is required");

                    IReadOnlyList<AvailabilityRangeRequest> ranges = request.Ranges ?? [];

                    SetAvailabilityResult result = await setAvailability.Execute(
                        publicId,
                        parsedParticipantId,
                        parsedGuestId,
                        request.Timezone,
                        ranges
                            .Select(r => new AvailabilityRangeResult(ParseWallClock(r.Start), ParseWallClock(r.End)))
                            .ToList()
                    );

                    return Results.Ok(new SetAvailabilityResponse(
                        result.ParticipantId,
                        result.Name,
                        result.Timezone,
                        result.Ranges
                            .Select(r => new AvailabilityRangeResponse(r.Start, r.End))
                            .ToList()
                    ));
                }
            )
            .WithName("SetAvailability")
            .Produces<SetAvailabilityResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static DateTime ParseWallClock(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            || parsed.Kind != DateTimeKind.Unspecified)
        {
            throw new InvalidAvailabilityRangeException(
                $"Availability range '{value}' must be a wall-clock datetime without a UTC offset.");
        }

        return parsed;
    }
}

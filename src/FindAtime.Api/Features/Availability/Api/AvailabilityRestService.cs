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

        app.MapGet(
                "/events/{publicId}/availabilities",
                async (string publicId, GetEventAvailabilities getEventAvailabilities) =>
                {
                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    GetEventAvailabilitiesResult result = await getEventAvailabilities.Execute(publicId);

                    return Results.Ok(new GetEventAvailabilitiesResponse(
                        result.EventTimezone,
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

        app.MapGet(
                "/events/{publicId}/participants/{participantId}/availability",
                async (string publicId, string participantId, GetParticipantAvailability getParticipantAvailability) =>
                {
                    if (string.IsNullOrWhiteSpace(publicId))
                        return Results.BadRequest("publicId is required");

                    if (!Guid.TryParse(participantId, out var parsedParticipantId))
                        return Results.BadRequest("participantId must be a valid uuid");

                    ParticipantAvailabilityResult result = await getParticipantAvailability.Execute(publicId, parsedParticipantId);

                    return Results.Ok(new ParticipantAvailabilityResponse(
                        result.ParticipantId,
                        result.Name,
                        result.Ranges
                            .Select(r => new AvailabilityRangeResponse(r.Start, r.End))
                            .ToList()
                    ));
                }
            )
            .WithName("GetParticipantAvailability")
            .Produces<ParticipantAvailabilityResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}

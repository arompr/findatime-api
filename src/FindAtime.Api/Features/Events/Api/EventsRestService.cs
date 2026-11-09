using System.Security.Claims;

public static class EventsRestService
{
    public const string JoinRateLimitPolicy = "join";

    public static void MapEvents(this WebApplication app)
    {
        app.MapPost(
                "/events",
                async (CreateEventRequestParams requestParams, ClaimsPrincipal user, CreateEvent createEvent) =>
                {
                    if (string.IsNullOrWhiteSpace(requestParams.OrganizerName))
                        return Results.BadRequest("organizerName is required");

                    var response = await createEvent.Execute(
                        requestParams.Name,
                        user.GetGuestId().ToString(),
                        requestParams.OrganizerName,
                        requestParams.IsPasscodeProtected,
                        string.IsNullOrWhiteSpace(requestParams.Timezone) ? null : requestParams.Timezone
                    );
                    return Results.Created($"/events/{response.PublicId}", response);
                }
            )
            .WithName("CreateEvent")
            .RequireAuthorization()
            .Produces<CreateEventResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet(
                "/events",
                async (ClaimsPrincipal user, ReadEventService readEventService) =>
                {
                    IReadOnlyList<EventSummaryDto> events = await readEventService.SearchEvents(user.GetGuestId());
                    var response = new SearchEventsResponse(
                        events.Select(e => new EventSummaryResponse(e.PublicId, e.Name, e.IsOrganizer)).ToList()
                    );
                    return Results.Ok(response);
                }
            )
            .WithName("SearchEvents")
            .RequireAuthorization()
            .Produces<SearchEventsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet(
                "/events/{publicId}",
                async (string publicId, ReadEventService readEventService) =>
                {
                    EventDto? dto = await readEventService.GetEventByPublicId(publicId);
                    if (dto is null)
                        throw new EventNotFoundException(publicId);

                    GetEventResponse response = new(
                        dto.PublicId,
                        dto.Name,
                        dto.IsPasscodeProtected,
                        dto.Timezone,
                        dto.IntervalMinutes,
                        dto.DayStartHour,
                        dto.DayEndHour
                    );
                    return Results.Ok(response);
                }
            )
            .WithName("GetEvent")
            .Produces<GetEventResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut(
                "/events/{publicId}/params",
                async (string publicId, UpdateEventParamsRequestParams requestParams, ClaimsPrincipal user, UpdateEventParams updateEventParams) =>
                {
                    if (requestParams.IntervalMinutes is null)
                        return Results.BadRequest("intervalMinutes is required");

                    if (requestParams.DayStartHour is null)
                        return Results.BadRequest("dayStartHour is required");

                    if (requestParams.DayEndHour is null)
                        return Results.BadRequest("dayEndHour is required");

                    UpdateEventParamsResponse response = await updateEventParams.Execute(
                        publicId,
                        user.GetGuestId(),
                        requestParams.Timezone,
                        requestParams.IntervalMinutes.Value,
                        requestParams.DayStartHour.Value,
                        requestParams.DayEndHour.Value
                    );

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateEventParams")
            .RequireAuthorization()
            .Produces<UpdateEventParamsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost(
                "/events/{publicId}/join",
                async (string publicId, JoinEventRequestParams requestParams, ClaimsPrincipal user, JoinEvent joinEvent) =>
                {
                    if (string.IsNullOrWhiteSpace(requestParams.ParticipantName))
                        return Results.BadRequest("participantName is required");

                    if (string.IsNullOrWhiteSpace(requestParams.Timezone))
                        return Results.BadRequest("timezone is required");

                    JoinEventResponse response = await joinEvent.Execute(
                        publicId,
                        requestParams.Passcode,
                        user.GetGuestId().ToString(),
                        requestParams.ParticipantName,
                        requestParams.Timezone
                    );

                    return Results.Created($"/events/{publicId}/participants/{response.ParticipantId}", response);
                }
            )
            .WithName("JoinEvent")
            .RequireAuthorization()
            .RequireRateLimiting(JoinRateLimitPolicy)
            .Produces<JoinEventResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        app.MapPost(
                "/events/{publicId}/leave",
                async (string publicId, ClaimsPrincipal user, LeaveEvent leaveEvent) =>
                {
                    await leaveEvent.Execute(publicId, user.GetGuestId().ToString());

                    return Results.NoContent();
                }
            )
            .WithName("LeaveEvent")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        app.MapDelete(
                "/events/{publicId}",
                async (string publicId, ClaimsPrincipal user, DeleteEvent deleteEvent) =>
                {
                    await deleteEvent.Execute(publicId, user.GetGuestId());

                    return Results.NoContent();
                }
            )
            .WithName("DeleteEvent")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}

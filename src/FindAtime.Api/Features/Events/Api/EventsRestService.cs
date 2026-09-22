public static class EventsRestService
{
    public static void MapEvents(this WebApplication app)
    {
        app.MapPost(
                "/events",
                async (CreateEventRequestParams requestParams, CreateEvent createEvent) =>
                {
                    if (!Guid.TryParse(requestParams.GuestId, out _))
                        return Results.BadRequest("guestId must be a valid uuid");

                    if (string.IsNullOrWhiteSpace(requestParams.OrganizerName))
                        return Results.BadRequest("organizerName is required");

                    var response = await createEvent.Execute(
                        requestParams.Name,
                        requestParams.GuestId,
                        requestParams.OrganizerName,
                        requestParams.IsPasscodeProtected
                    );
                    return Results.Created($"/events/{response.PublicId}", response);
                }
            )
            .WithName("CreateEvent")
            .Produces<CreateEventResponse>(StatusCodes.Status201Created);

        app.MapGet(
                "/events",
                async (string? guestId, ReadEventService readEventService) =>
                {
                    if (string.IsNullOrWhiteSpace(guestId))
                        return Results.BadRequest("guestId is required");

                    if (!Guid.TryParse(guestId, out var parsed))
                        return Results.BadRequest("guestId must be a valid uuid");

                    IReadOnlyList<EventSummaryDto> events = await readEventService.SearchEvents(parsed);
                    var response = new SearchEventsResponse(
                        events.Select(e => new EventSummaryResponse(e.PublicId, e.Name, e.IsOrganizer)).ToList()
                    );
                    return Results.Ok(response);
                }
            )
            .WithName("SearchEvents")
            .Produces<SearchEventsResponse>(StatusCodes.Status200OK);

        app.MapGet(
                "/events/{publicId}",
                async (string publicId, ReadEventService readEventService) =>
                {
                    EventDto? dto = await readEventService.GetEventByPublicId(publicId);
                    if (dto is null)
                        throw new EventNotFoundException(publicId);

                    GetEventResponse response = new(dto.PublicId, dto.Name, dto.IsPasscodeProtected);
                    return Results.Ok(response);
                }
            )
            .WithName("GetEvent")
            .Produces<GetEventResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost(
                "/events/{publicId}/join",
                async (string publicId, JoinEventRequestParams requestParams, JoinEvent joinEvent) =>
                {
                    if (!Guid.TryParse(requestParams.GuestId, out _))
                        return Results.BadRequest("guestId must be a valid uuid");

                    if (string.IsNullOrWhiteSpace(requestParams.ParticipantName))
                        return Results.BadRequest("participantName is required");

                    JoinEventResponse response = await joinEvent.Execute(
                        publicId,
                        requestParams.Passcode,
                        requestParams.GuestId,
                        requestParams.ParticipantName
                    );

                    return Results.Created($"/events/{publicId}/participants/{response.ParticipantId}", response);
                }
            )
            .WithName("JoinEvent")
            .Produces<JoinEventResponse>(StatusCodes.Status201Created);

        app.MapPost(
                "/events/{publicId}/leave",
                async (string publicId, LeaveEventRequestParams requestParams, LeaveEvent leaveEvent) =>
                {
                    if (!Guid.TryParse(requestParams.GuestId, out _))
                        return Results.BadRequest("guestId must be a valid uuid");

                    await leaveEvent.Execute(publicId, requestParams.GuestId);

                    return Results.NoContent();
                }
            )
            .WithName("LeaveEvent")
            .Produces(StatusCodes.Status204NoContent);
    }
}

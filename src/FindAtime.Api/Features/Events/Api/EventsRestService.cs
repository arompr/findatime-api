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

                    var response = await createEvent.Execute(requestParams.Name, requestParams.GuestId, requestParams.OrganizerName);
                    return Results.Created($"/events/{response.EventId}", response);
                }
            )
            .WithName("CreateEvent");

        app.MapGet(
                "/events/{id}",
                async (Guid id, GetEvent getEvent) =>
                {
                    EventReadDto? eventReadDto = await getEvent.Execute(id);
                    return eventReadDto is null ? Results.NotFound() : Results.Ok(eventReadDto);
                }
            )
            .WithName("GetEvent");

        app.MapGet(
                "/events/by-public-id/{publicId}",
                async (string publicId, GetEventByPublicId getEventByPublicId) =>
                {
                    GetEventByPublicIdResponse? response = await getEventByPublicId.Execute(publicId);
                    return response is null ? Results.NotFound() : Results.Ok(response);
                }
            )
            .WithName("GetEventByPublicId");

        app.MapPost(
                "/events/{id}/join",
                async (Guid id, JoinEventRequestParams requestParams, JoinEvent joinEvent) =>
                {
                    if (!Guid.TryParse(requestParams.GuestId, out _))
                        return Results.BadRequest("guestId must be a valid uuid");

                    if (string.IsNullOrWhiteSpace(requestParams.ParticipantName))
                        return Results.BadRequest("participantName is required");

                    if (string.IsNullOrWhiteSpace(requestParams.Passcode))
                        return Results.BadRequest("passcode is required");

                    JoinEventResult result = await joinEvent.Execute(
                        id, requestParams.Passcode, requestParams.GuestId, requestParams.ParticipantName);

                    return result.Status switch
                    {
                        JoinEventStatus.Joined => Results.Created(
                            $"/events/{id}/participants/{result.Response!.ParticipantId}", result.Response),
                        JoinEventStatus.AlreadyJoined => Results.Ok(result.Response),
                        JoinEventStatus.InvalidPasscode => Results.Json(
                            new { error = "invalid_passcode" }, statusCode: StatusCodes.Status401Unauthorized),
                        JoinEventStatus.NameMismatch => Results.Json(
                            new { error = "name_mismatch" }, statusCode: StatusCodes.Status409Conflict),
                        JoinEventStatus.EventNotFound => Results.NotFound(),
                        _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
                    };
                }
            )
            .WithName("JoinEvent");

        app.MapPost(
                "/events/{id}/leave",
                async (Guid id, LeaveEventRequestParams requestParams, LeaveEvent leaveEvent) =>
                {
                    if (!Guid.TryParse(requestParams.GuestId, out _))
                        return Results.BadRequest("guestId must be a valid uuid");

                    LeaveEventResult result = await leaveEvent.Execute(id, requestParams.GuestId);

                    return result.Status switch
                    {
                        LeaveEventStatus.Left => Results.NoContent(),
                        LeaveEventStatus.NotFound => Results.NotFound(),
                        LeaveEventStatus.OrganizerCannotLeave => Results.Json(
                            new { error = "organizer_cannot_leave" }, statusCode: StatusCodes.Status409Conflict),
                        _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
                    };
                }
            )
            .WithName("LeaveEvent");
    }
}

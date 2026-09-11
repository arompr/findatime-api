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
                    EventReadDto eventReadDto = await getEvent.Execute(id);
                    return Results.Ok(eventReadDto);
                }
            )
            .WithName("GetEvent");

        app.MapGet(
                "/events/by-public-id/{publicId}",
                async (string publicId, GetEventByPublicId getEventByPublicId) =>
                {
                    GetEventByPublicIdResponse response = await getEventByPublicId.Execute(publicId);
                    return Results.Ok(response);
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

                    JoinEventResponse response = await joinEvent.Execute(
                        id, requestParams.Passcode, requestParams.GuestId, requestParams.ParticipantName);

                    return Results.Created($"/events/{id}/participants/{response.ParticipantId}", response);
                }
            )
            .WithName("JoinEvent");

        app.MapPost(
                "/events/{id}/leave",
                async (Guid id, LeaveEventRequestParams requestParams, LeaveEvent leaveEvent) =>
                {
                    if (!Guid.TryParse(requestParams.GuestId, out _))
                        return Results.BadRequest("guestId must be a valid uuid");

                    await leaveEvent.Execute(id, requestParams.GuestId);

                    return Results.NoContent();
                }
            )
            .WithName("LeaveEvent");
    }
}

public static class EventsRestService
{
    public static void MapEvents(this WebApplication app)
    {
        app.MapPost(
                "/events",
                async (CreateEventRequestParams requestParams, CreateEvent createEvent) =>
                {
                    if (!Guid.TryParse(requestParams.ParticipantUuid, out _))
                        return Results.BadRequest("participantUuid must be a valid uuid");

                    if (string.IsNullOrWhiteSpace(requestParams.ParticipantName))
                        return Results.BadRequest("participantName is required");

                    var eventId = await createEvent.Execute(requestParams.Name, requestParams.ParticipantUuid, requestParams.ParticipantName);
                    return Results.Created($"/events/{eventId}", eventId);
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
    }
}

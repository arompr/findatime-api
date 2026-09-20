using Microsoft.AspNetCore.Diagnostics;

public sealed class ExceptionHandler : IExceptionHandler
{
    private static readonly Dictionary<Type, (int Status, string Error)> Mappings = new()
    {
        [typeof(EventNotFoundException)] = (StatusCodes.Status404NotFound, "event_not_found"),
        [typeof(InvalidPasscodeException)] = (StatusCodes.Status401Unauthorized, "invalid_passcode"),
        [typeof(ParticipantAlreadyJoinedException)] = (StatusCodes.Status409Conflict, "participant_already_joined"),
        [typeof(OrganizerCannotLeaveException)] = (StatusCodes.Status409Conflict, "organizer_cannot_leave"),
        [typeof(ParticipantNotFoundException)] = (StatusCodes.Status404NotFound, "participant_not_found"),
        [typeof(InvalidAvailabilityRangeException)] = (StatusCodes.Status400BadRequest, "invalid_availability_range"),
        [typeof(ForbiddenException)] = (StatusCodes.Status403Forbidden, "forbidden"),
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!Mappings.TryGetValue(exception.GetType(), out var mapping))
            return false;

        context.Response.StatusCode = mapping.Status;

        await context.Response.WriteAsJsonAsync(
            new { message = exception.Message, error = mapping.Error },
            cancellationToken);

        return true;
    }
}

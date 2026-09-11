public sealed class OrganizerCannotLeaveException : Exception
{
    public OrganizerCannotLeaveException(Guid eventId)
        : base($"The organizer of event '{eventId}' cannot leave their own event.")
    {
    }
}

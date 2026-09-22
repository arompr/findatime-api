public sealed class OrganizerCannotLeaveException : Exception
{
    public OrganizerCannotLeaveException(string publicId)
        : base($"The organizer of event with public id '{publicId}' cannot leave their own event.")
    {
    }
}

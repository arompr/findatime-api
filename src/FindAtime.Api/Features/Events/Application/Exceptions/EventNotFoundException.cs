public sealed class EventNotFoundException : Exception
{
    public EventNotFoundException(Guid eventId)
        : base($"Event '{eventId}' was not found.")
    {
    }

    public EventNotFoundException(string publicId)
        : base($"Event with public id '{publicId}' was not found.")
    {
    }
}

public sealed class EventNotFoundException : Exception
{
    public EventNotFoundException(string publicId)
        : base($"Event with public id '{publicId}' was not found.")
    {
    }
}

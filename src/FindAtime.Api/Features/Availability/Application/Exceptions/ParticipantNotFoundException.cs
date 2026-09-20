public sealed class ParticipantNotFoundException : Exception
{
    public ParticipantNotFoundException(string publicId, Guid guestId)
        : base($"Participant with guest id '{guestId}' was not found for event '{publicId}'.")
    {
    }
}

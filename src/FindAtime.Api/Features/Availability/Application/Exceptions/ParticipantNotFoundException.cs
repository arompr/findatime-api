public sealed class ParticipantNotFoundException : Exception
{
    public ParticipantNotFoundException(string publicId, Guid guestId)
        : base($"Participant with guest id '{guestId}' was not found for event '{publicId}'.")
    {
    }

    public ParticipantNotFoundException(string publicId, string participantId)
        : base($"Participant '{participantId}' was not found for event '{publicId}'.")
    {
    }
}

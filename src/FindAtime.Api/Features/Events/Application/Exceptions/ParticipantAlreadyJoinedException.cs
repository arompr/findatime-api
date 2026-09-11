public sealed class ParticipantAlreadyJoinedException : Exception
{
    public ParticipantAlreadyJoinedException(Guid eventId)
        : base($"This participant has already joined event '{eventId}'.")
    {
    }
}

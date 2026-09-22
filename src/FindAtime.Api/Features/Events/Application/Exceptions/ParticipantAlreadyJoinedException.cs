public sealed class ParticipantAlreadyJoinedException : Exception
{
    public ParticipantAlreadyJoinedException(string publicId)
        : base($"This participant has already joined event with public id '{publicId}'.")
    {
    }
}

public sealed class InvalidPasscodeException : Exception
{
    public InvalidPasscodeException(Guid eventId)
        : base($"Invalid passcode for event '{eventId}'.")
    {
    }
}

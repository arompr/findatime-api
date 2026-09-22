public sealed class InvalidPasscodeException : Exception
{
    public InvalidPasscodeException(string publicId)
        : base($"Invalid passcode for event with public id '{publicId}'.")
    {
    }
}

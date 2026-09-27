public sealed class InvalidTimezoneException : Exception
{
    public InvalidTimezoneException(string ianaId)
        : base($"Timezone '{ianaId}' is not a valid IANA timezone.")
    {
    }
}

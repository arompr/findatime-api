public sealed class InvalidIntervalException : Exception
{
    public InvalidIntervalException()
        : base("IntervalMinutes must be one of: 15, 30, 60.")
    {
    }
}

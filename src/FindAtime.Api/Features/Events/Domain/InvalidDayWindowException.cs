public sealed class InvalidDayWindowException : Exception
{
    public InvalidDayWindowException()
        : base("Day window must satisfy 0 <= dayStartHour < dayEndHour <= 24.")
    {
    }
}

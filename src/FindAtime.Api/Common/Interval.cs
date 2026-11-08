public static class Interval
{
    public const int DefaultMinutes = 30;

    public static readonly IReadOnlySet<int> Allowed = new HashSet<int> { 15, 30, 60 };

    public static bool IsValid(int minutes)
    {
        return Allowed.Contains(minutes);
    }

    public static void Validate(int minutes)
    {
        if (!IsValid(minutes))
            throw new InvalidIntervalException();
    }

    public static DateTime Floor(DateTime wallClock, int minutes)
    {
        Validate(minutes);

        long ticksPerInterval = (long)minutes * TimeSpan.TicksPerMinute;
        long timeOfDayTicks = wallClock.TimeOfDay.Ticks;
        long snappedTicks = (timeOfDayTicks / ticksPerInterval) * ticksPerInterval;

        return DateTime.SpecifyKind(
            wallClock.Date + TimeSpan.FromTicks(snappedTicks),
            DateTimeKind.Unspecified);
    }

    public static DateTime Ceil(DateTime wallClock, int minutes)
    {
        Validate(minutes);

        long ticksPerInterval = (long)minutes * TimeSpan.TicksPerMinute;
        long timeOfDayTicks = wallClock.TimeOfDay.Ticks;
        long snappedTicks = ((timeOfDayTicks + ticksPerInterval - 1) / ticksPerInterval) * ticksPerInterval;

        return DateTime.SpecifyKind(
            wallClock.Date + TimeSpan.FromTicks(snappedTicks),
            DateTimeKind.Unspecified);
    }
}

public readonly record struct EventParams
{
    public string? Timezone { get; init; }

    public int IntervalMinutes { get; init; }

    public int DayStartHour { get; init; }

    public int DayEndHour { get; init; }

    public EventParams(string? timezone, int intervalMinutes, int dayStartHour, int dayEndHour)
    {
        Interval.Validate(intervalMinutes);
        DayWindow.Validate(dayStartHour, dayEndHour);
        this.Timezone = timezone;
        this.IntervalMinutes = intervalMinutes;
        this.DayStartHour = dayStartHour;
        this.DayEndHour = dayEndHour;
    }
}

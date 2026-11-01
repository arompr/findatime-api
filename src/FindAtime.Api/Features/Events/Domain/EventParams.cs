public readonly record struct EventParams
{
    public string? Timezone { get; init; }

    public int IntervalMinutes { get; init; }

    public EventParams(string? timezone, int intervalMinutes)
    {
        Interval.Validate(intervalMinutes);
        this.Timezone = timezone;
        this.IntervalMinutes = intervalMinutes;
    }
}

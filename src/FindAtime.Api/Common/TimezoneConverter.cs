public sealed class TimezoneConverter
{
    public TimeZoneInfo Resolve(string ianaId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidTimezoneException(ianaId);
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidTimezoneException(ianaId);
        }
    }

    public DateTimeOffset ToUtc(DateTime wallClock, string ianaId)
    {
        var zone = Resolve(ianaId);

        var local = wallClock.Kind == DateTimeKind.Unspecified
            ? wallClock
            : DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new InvalidAvailabilityRangeException(
                $"Local time '{local:yyyy-MM-ddTHH:mm:ss}' is invalid or ambiguous in timezone '{ianaId}'.");

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return new DateTimeOffset(utc);
    }

    public DateTime ToWallClock(DateTimeOffset instant, string ianaId)
    {
        var zone = Resolve(ianaId);
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return DateTime.SpecifyKind(local.DateTime, DateTimeKind.Unspecified);
    }
}

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
}

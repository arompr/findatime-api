public static class DayWindow
{
    public const int DefaultStartHour = 9;

    public const int DefaultEndHour = 21;

    public static bool IsValid(int startHour, int endHour)
    {
        return startHour >= 0 && endHour <= 24 && startHour < endHour;
    }

    public static void Validate(int startHour, int endHour)
    {
        if (!IsValid(startHour, endHour))
            throw new InvalidDayWindowException();
    }
}

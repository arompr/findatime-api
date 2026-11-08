public record EventDto(string PublicId, string Name, string? Timezone, int IntervalMinutes, int DayStartHour, int DayEndHour, bool IsPasscodeProtected);

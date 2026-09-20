public class AvailabilityFactory
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromHours(24);

    public Availability Create(
        ParticipantId participantId,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        if (end <= start)
            throw new InvalidAvailabilityRangeException("Availability range must end after it starts.");

        if (end - start > MaxDuration)
            throw new InvalidAvailabilityRangeException("Availability range cannot exceed 24 hours.");

        return new Availability(
            AvailabilityId.FromString(Guid.NewGuid().ToString()),
            participantId,
            start,
            end,
            DateTimeOffset.UtcNow
        );
    }
}

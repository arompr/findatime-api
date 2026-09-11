public enum LeaveEventStatus
{
    Left,
    NotFound,
    OrganizerCannotLeave,
}

public record LeaveEventResult(LeaveEventStatus Status);

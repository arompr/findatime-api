public enum JoinEventStatus
{
    Joined,
    AlreadyJoined,
    InvalidPasscode,
    NameMismatch,
    EventNotFound,
}

public record JoinEventResult(JoinEventStatus Status, JoinEventResponse? Response);

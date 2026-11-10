public sealed class GuestAlreadyClaimedException : Exception
{
    public GuestAlreadyClaimedException(Guid guestId)
        : base($"Guest id '{guestId}' has already been claimed.")
    {
    }
}

using System.Security.Claims;

public static class CurrentGuest
{
    public const string GuestIdClaim = "guest_id";

    public static Guid GetGuestId(this ClaimsPrincipal user)
    {
        string? value = user.FindFirstValue(GuestIdClaim);
        if (value is null || !Guid.TryParse(value, out var guestId))
            throw new InvalidOperationException("The request is not authenticated as a guest.");

        return guestId;
    }
}

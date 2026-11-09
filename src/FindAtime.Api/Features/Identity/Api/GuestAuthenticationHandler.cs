using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public sealed class GuestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Guest";

    private static readonly TimeSpan LastSeenResolution = TimeSpan.FromHours(1);

    private readonly ReadGuestService _readGuestService;
    private readonly GuestRepository _guestRepository;
    private readonly GuestTokenHasher _tokenHasher;
    private readonly TimeProvider _timeProvider;

    public GuestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ReadGuestService readGuestService,
        GuestRepository guestRepository,
        GuestTokenHasher tokenHasher,
        TimeProvider timeProvider
    )
        : base(options, logger, encoder)
    {
        _readGuestService = readGuestService;
        _guestRepository = guestRepository;
        _tokenHasher = tokenHasher;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
            return AuthenticateResult.NoResult();

        const string prefix = SchemeName + " ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        string token = header[prefix.Length..].Trim();
        if (token.Length == 0)
            return AuthenticateResult.Fail("Guest token is empty.");

        GuestDto? guest = await _readGuestService.FindByTokenHash(_tokenHasher.Hash(token));
        if (guest is null)
            return AuthenticateResult.Fail("Guest token is invalid.");

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (now - guest.LastSeenAt >= LastSeenResolution)
            await _guestRepository.TouchLastSeen(GuestId.FromString(guest.GuestId.ToString()), now);

        var identity = new ClaimsIdentity(
            [new Claim(CurrentGuest.GuestIdClaim, guest.GuestId.ToString())],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new
        {
            message = "A valid guest token is required (Authorization: Guest <token>).",
            error = "unauthorized",
        });
    }
}

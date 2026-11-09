# 009-guest-tokens: Design

## Summary

Adds an `Identity` feature that owns guests and their tokens, plugs a custom
ASP.NET authentication scheme (`Guest`) into the pipeline, and moves every
"who is calling" decision from request payloads to the authenticated principal.
`GuestId` remains the identity key referenced by `participants.guest_id`; a
guest row is the credential attached to it.

## Changes

### Persistence
- New table `guests (id uuid PK, token_hash bytea UNIQUE NOT NULL,
  created_at timestamptz, last_seen_at timestamptz)`, EF migration `AddGuests`.
- No FK from `participants.guest_id` to `guests.id`: legacy guest ids have no
  guest row until adopted.
- `Guest` is mapped in the shared `DbContext` (`DbSet<Guest> Guests`).

### Identity feature (`Features/Identity/`)
- Domain: `Guest` entity (`GuestId Id`, `TokenHash`, `CreatedAt`, `LastSeenAt`).
- Infra:
  - `GuestTokenGenerator`: 32 random bytes, base64url (43 chars).
  - `GuestTokenHasher`: SHA-256 of the token. Tokens are high-entropy, so a
    fast unsalted hash is sufficient and allows lookup by hash.
  - `GuestRepository` (EF): add, exists, touch `last_seen_at`, and "does any
    participant use this guest id".
  - `ReadGuestService` (Dapper, `get_guest_by_token_hash.sql`): token-hash →
    guest id + last seen, used on every authenticated request.
- Application:
  - `CreateGuest` → new guest with a random id.
  - `AdoptLegacyGuest(Guid)` → REQ-002/003/004. A unique-violation on insert
    (concurrent adoption) is reported as `GuestAlreadyClaimedException` (409).
- Api: `POST /guests` (`CreateGuestRequestParams { legacyGuestId? }` →
  `CreateGuestResponse { guestToken }`). The `Identity:AllowLegacyAdoption`
  flag (default `true`) gates adoption in the endpoint.

### Authentication
- `GuestAuthenticationHandler` (scheme `Guest`) parses
  `Authorization: Guest <token>`, looks the hash up through `ReadGuestService`,
  and issues a principal with a `guest_id` claim. It touches `last_seen_at` at
  most once per hour. No header / other scheme → no result; unknown token →
  failure. The challenge writes `401 { message, error: "unauthorized" }`.
- `CurrentGuest.GetGuestId(this ClaimsPrincipal)` returns the caller's id;
  endpoints take `ClaimsPrincipal user` and call `.RequireAuthorization()`.
- `Program.cs`: `AddAuthentication`/`AddAuthorization`, `UseAuthentication`/
  `UseAuthorization` after CORS.

### API contract
- Removed: `guestId` from `CreateEventRequestParams` and
  `JoinEventRequestParams`; `LeaveEventRequestParams` (leave has no body);
  `?guestId=` on `GET /events`; `X-Guest-Id` everywhere.
- `RequireAuthorization` on every events/availability endpoint except
  `GET /events/{publicId}` and `/health`.
- Existing use-case signatures keep their `string`/`Guid` guest parameters;
  only the endpoint changes where the value comes from. This keeps the change
  out of the domain and the 190+ existing test call sites.
- `GetEventAvailabilities` and `GetParticipantAvailability` gain a `Guid
  guestId` parameter and check membership with
  `ReadAvailabilityService.GetParticipantByEventAndGuest` after the event
  lookup (404 before 403).

### Rate limiting
- `AddRateLimiter` with two fixed-window policies partitioned by remote IP:
  `guests` and `join`, limits from `RateLimiting:GuestsPerMinute` /
  `RateLimiting:JoinsPerMinute` (defaults 10; Development 1000 so Hurl runs
  don't trip them). Rejections write `429 { error: "rate_limited" }`.
- `UseForwardedHeaders` (X-Forwarded-For/Proto, known proxies cleared) so the
  client IP is the one Render's proxy appended.

### OpenAPI
- Document transformer registers an `apiKey`-in-header security scheme `Guest`
  (`Authorization`, value `Guest <token>`); an operation transformer adds the
  requirement to operations whose endpoint has authorization metadata, plus a
  `401` response.

## Migration / Rollout

1. Apply migration `AddGuests` (additive).
2. Deploy the API together with the UI change that obtains a token from
   `POST /guests` (passing its old localStorage guest id once) and sends
   `Authorization: Guest <token>`. The API is not backwards compatible with the
   current UI: deploy both together.
3. Regenerate and publish the TypeScript client (`make typescript-client`).
4. Later change: set `Identity:AllowLegacyAdoption=false`, then remove it.

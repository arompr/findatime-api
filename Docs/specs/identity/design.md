# Identity Design

## Structure

`Features/Identity/` follows the usual slices:

- Domain: `Guest` (EF-mapped in the shared `DbContext`, table `guests`).
- Infra: `GuestTokenGenerator` (`RandomNumberGenerator`, base64url),
  `GuestTokenHasher` (SHA-256), `GuestRepository` (EF: save, exists,
  participations check, `TouchLastSeen` via `ExecuteUpdateAsync`),
  `ReadGuestService` (Dapper, `get_guest_by_token_hash.sql`).
- Application: `CreateGuest`, `AdoptLegacyGuest`,
  `GuestAlreadyClaimedException` → 409 `guest_already_claimed`.
- Api: `GuestAuthenticationHandler`, `CurrentGuest`, `IdentityRestService`
  (`POST /guests`).

## Persistence

```
guests
  - id (uuid, PK)
  - token_hash (bytea, unique)
  - created_at, last_seen_at (timestamptz)
```

There is deliberately no FK from `participants.guest_id` to `guests.id`: legacy
guest ids exist on participants before (or without) a guest row. A fast,
unsalted hash is correct for 256-bit random tokens and allows lookup by hash.
A concurrent second adoption hits the primary key and is reported as
`GuestAlreadyClaimedException`.

## Authentication pipeline

`Program.cs` registers the `Guest` authentication scheme
(`GuestAuthenticationHandler`) as the default, plus authorization, forwarded
headers and the rate limiter:

```
UseForwardedHeaders → UseExceptionHandler → UseCors → UseAuthentication
  → UseAuthorization → UseRateLimiter → endpoints
```

The handler issues a principal with a `guest_id` claim; endpoints that need a
caller declare `.RequireAuthorization()` and read it with
`ClaimsPrincipal.GetGuestId()`. The handler's challenge writes the standard
`{ message, error }` body.

Forwarded headers trust `X-Forwarded-For`/`-Proto` from any proxy (Render's
proxy addresses are not fixed); with the default forward limit of 1 the client
IP is the entry the platform proxy appended.

## Rate limiting

Two fixed-window (1 minute) policies partitioned by client IP:
`guests` (`POST /guests`) and `join` (`POST /events/{publicId}/join`). Limits
come from `RateLimiting:GuestsPerMinute` / `RateLimiting:JoinsPerMinute`
(10 by default; 1000 in Development so Hurl runs don't trip them). Rejections
are 429 `{ error: "rate_limited" }`. Limiter state is in-memory, per instance.

## OpenAPI

A document transformer declares the `Guest` security scheme (`apiKey` in the
`Authorization` header, value `Guest <token>`); an operation transformer adds it
to every operation with authorization metadata.

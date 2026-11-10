# Identity

## Purpose

Identity issues and verifies guest tokens. A guest is the caller identity used
by every other feature: an event's participants reference guests by
`GuestId`.

## Invariants

- A guest token is 32 random bytes, base64url-encoded (43 characters).
- Only the SHA-256 hash of a token is stored; hashes are unique.
- A guest id is never returned by the API and never accepted from a request.
- A legacy guest id can be adopted at most once.

## State

A guest has:

- GuestId (UUID; equals the legacy browser id when adopted)
- TokenHash
- CreatedAt
- LastSeenAt (refreshed at most once per hour when the token is used)

## Behavior

### Authenticate

A request carrying `Authorization: Guest <token>` whose token hash matches a
guest is made by that guest. A missing header, another scheme or an unknown
token is unauthenticated; endpoints that need a caller then answer
401 `{ message, error: "unauthorized" }`.

### Create a guest

Creates a guest with a random id and returns its token once.

### Adopt a legacy guest id

When `Identity:AllowLegacyAdoption` is on and the request names a legacy guest
id: if a guest with that id exists → 409 `guest_already_claimed`; if no
participant uses that id → a fresh guest is created as above; otherwise a guest
is created with that id. When adoption is off, the legacy id is ignored.

## API

### Create a guest
`POST /guests`

Optional body `{ legacyGuestId }`. Returns 201 `{ guestToken }`. 400 when
`legacyGuestId` is not a uuid, 409 `guest_already_claimed`, 429 `rate_limited`
above `RateLimiting:GuestsPerMinute` per client IP.

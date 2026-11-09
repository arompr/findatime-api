# 009-guest-tokens: Tasks

Landed: 2026-11-09T23:36:23Z

Ordered. Each task depends on the ones above it unless marked `[P]`.

Each task gets a `Verified: <iso>` stamp once its **Verification** passes in the
verify phase. A fully landed change carries a `Landed: <iso>` line under its H1.

Environment:

- Integration tests need a working Podman/Docker socket (Testcontainers).
- Hurl needs the API on `:5263` against a migrated local DB.

## Task 1 — Guests persistence and token primitives

**Depends on:** none

**Goal:** guests can be stored and looked up by token hash.

**Work:**
- `Features/Identity/Domain/Guest.cs`; `Infra/GuestTokenGenerator.cs`,
  `Infra/GuestTokenHasher.cs`, `Infra/GuestRepository.cs`,
  `Infra/Read/ReadGuestService.cs` + `get_guest_by_token_hash.sql` (embedded).
- `DbSet<Guest>` + mapping in `DbContext`; migration `AddGuests`.
- Unit tests: generator (length, alphabet, uniqueness), hasher (deterministic,
  32 bytes, differs per token).

**Acceptance criteria:**
- [x] Migration creates `guests` with a unique `token_hash`.
- [x] Only the hash is persisted.

**Verification:**
- `dotnet test tests/FindAtime.UnitTests --filter "GuestToken"`

Verified: 2026-11-09T23:36:23Z

## Task 2 — CreateGuest / AdoptLegacyGuest use cases

**Depends on:** Task 1

**Goal:** REQ-001..004.

**Work:**
- `Application/CreateGuest.cs`, `Application/AdoptLegacyGuest.cs`,
  `Application/Exceptions/GuestAlreadyClaimedException.cs` (409 mapping).
- Register in both DI graphs.
- Integration tests `CreateGuestTests`, `AdoptLegacyGuestTests` (token resolves
  through `ReadGuestService`; adopt once; second adopt 409; unknown id → fresh
  guest; adopted guest sees its events through `SearchEvents`).

**Acceptance criteria:**
- [x] SC-002 and SC-003 hold at the use-case level.

**Verification:**
- `dotnet test tests/FindAtime.IntegrationTests --filter "CreateGuestTests|AdoptLegacyGuestTests"`

Verified: 2026-11-09T23:36:23Z

## Task 3 — Guest authentication and endpoint migration

**Depends on:** Task 2

**Goal:** REQ-005..009.

**Work:**
- `Features/Identity/Api/GuestAuthenticationHandler.cs`, `CurrentGuest.cs`,
  `IdentityRestService.cs` (`POST /guests`), request/response records.
- `Program.cs` auth wiring; `MapIdentity()`.
- Events/availability endpoints: `ClaimsPrincipal` + `RequireAuthorization`;
  drop `guestId` fields/query and `X-Guest-Id`; delete `LeaveEventRequestParams`.
- `GetEventAvailabilities` / `GetParticipantAvailability`: membership check.
- Update their integration tests; add outsider-forbidden and unknown-event tests.
- OpenAPI security scheme + `OpenApiDocumentTests` (scheme present, no
  `guestId` in request schemas, no `guestId` query on `GET /events`).

**Acceptance criteria:**
- [x] SC-004, SC-005, SC-006 hold.
- [x] No `X-Guest-Id` / `guestId` input remains in the API.

**Verification:**
- `dotnet build`
- `dotnet test`

Verified: 2026-11-09T23:36:23Z

## Task 4 — Rate limiting

**Depends on:** Task 3

**Goal:** REQ-010.

**Work:**
- `AddRateLimiter` policies `guests` and `join`, `UseForwardedHeaders`,
  `RateLimiting` config (Development: high limits); `429 rate_limited` body.

**Acceptance criteria:**
- [x] Exceeding the limit returns 429.

**Verification:**
- `dotnet build`; manual: run the API with `RateLimiting__GuestsPerMinute=2`
  and call `POST /guests` three times.

Verified: 2026-11-09T23:36:23Z

## Task 5 — Hurl files and docs

**Depends on:** Task 4

**Goal:** wire-level coverage and documentation.

**Work:**
- New `Features/Identity/Api/identity.hurl`; rewrite `events.hurl` and
  `availability.hurl` to create guests, capture tokens and send
  `Authorization`; add 401/403 entries.
- README Hurl section mentions `identity.hurl`.

**Acceptance criteria:**
- [x] All three Hurl files pass against a local API.

**Verification:**
- `hurl --test src/FindAtime.Api/Features/*/Api/*.hurl`

Verified: 2026-11-09T23:36:23Z

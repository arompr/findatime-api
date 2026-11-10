# 009-guest-tokens: Verification

**Status:** PASS

## Summary

All five tasks verified. `dotnet test` passes (161 unit, 114 integration). The
three Hurl files pass (37 requests) against the branch's API on a throwaway
Postgres 18 container migrated from scratch. Legacy adoption, the second-adoption
409 and both rate limits were exercised over HTTP.

## Task results

- Task 1 — Guests persistence and token primitives: PASS — `GuestTokenGeneratorTests`,
  `GuestTokenHasherTests` (5 passed); migration `AddGuests` applied cleanly to an
  empty database together with all earlier migrations.
- Task 2 — CreateGuest / AdoptLegacyGuest: PASS — `CreateGuestTests`,
  `AdoptLegacyGuestTests` (7 passed).
- Task 3 — Guest authentication and endpoint migration: PASS — full `dotnet test`;
  `OpenApiDocumentTests` assert the security scheme, per-operation requirements,
  public `GET /events/{publicId}` and `POST /guests`, and no `guestId` inputs.
- Task 4 — Rate limiting: PASS — with `RateLimiting__JoinsPerMinute=3`, the 4th+
  joins in a window got 429; `POST /guests` past 10/min got 429
  `{ error: "rate_limited" }`.
- Task 5 — Hurl files and docs: PASS — `identity.hurl` (6), `events.hurl` (14),
  `availability.hurl` (17) all succeed.

## Acceptance criteria

- [x] `POST /guests` returns a token; the database stores only its hash — `CreateGuestTests.Execute_ShouldPersistOnlyTheTokenHash`.
- [x] Legacy adoption succeeds once, then 409; unknown ids get a fresh guest — integration tests + HTTP check against a seeded legacy participant.
- [x] No endpoint accepts `X-Guest-Id`, body `guestId` or query `guestId` — OpenAPI tests; grep of `src/` finds no `X-Guest-Id`.
- [x] Missing/invalid token → 401 — `identity.hurl` entries 3–4, `events.hurl` entry 12.
- [x] Non-participants get 403 on both availability reads — integration tests + `availability.hurl` entries 9, 11.
- [x] `GET /events/{publicId}` remains public — `events.hurl` entry 5, OpenAPI test.
- [x] Join and guest creation are rate-limited per IP (429) — manual HTTP check.
- [x] OpenAPI declares the `Guest` security scheme — `OpenApiDocumentTests`.

## Spec conformance

REQ-001 … REQ-010 satisfied; SC-001 … SC-006 covered by integration tests and/or Hurl.

## Design conformance

Followed, with one recorded simplification: the plan's `ICurrentGuest` service is a
`ClaimsPrincipal.GetGuestId()` extension (`CurrentGuest`), and existing use cases keep
their `string`/`Guid` guest parameters (as the design states).

## Issues found

- None blocking. Note for rollout: the API is not backwards compatible with the current
  UI; the UI change and the TypeScript client regeneration must ship together.

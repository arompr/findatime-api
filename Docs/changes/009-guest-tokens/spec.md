# 009-guest-tokens: Spec

## Requirements

### REQ-001: Issue a guest token
WHEN a client calls `POST /guests` without a legacy guest id THEN THE SYSTEM
SHALL create a new guest and return `201 { guestToken }`, where the token is an
opaque random secret, and SHALL persist only a hash of it. The guest id is never
returned.

### REQ-002: Adopt a legacy guest id
WHEN a client calls `POST /guests` with `{ legacyGuestId }`, legacy adoption is
enabled, at least one participant has that guest id and no guest exists with
that id THEN THE SYSTEM SHALL create the guest with that id and return a token
for it.

### REQ-003: Legacy id already claimed
WHEN a client calls `POST /guests` with a `legacyGuestId` that already belongs
to a guest THEN THE SYSTEM SHALL respond `409 guest_already_claimed`.

### REQ-004: Unknown legacy id
WHEN `legacyGuestId` matches no participant, or legacy adoption is disabled,
THEN THE SYSTEM SHALL create a fresh guest as in REQ-001.

### REQ-005: Authenticate the caller
WHEN a request carries `Authorization: Guest <token>` for an existing guest
THEN THE SYSTEM SHALL treat the request as made by that guest.

### REQ-006: Reject unauthenticated calls
WHEN an endpoint that needs a caller receives no token, a malformed header or an
unknown token THEN THE SYSTEM SHALL respond `401 unauthorized`.

### REQ-007: Endpoints use the authenticated guest
WHEN creating, searching ("my events"), joining, leaving, updating params,
deleting, resolving "my participant" or setting availability THEN THE SYSTEM
SHALL use the authenticated guest and SHALL NOT accept a guest id from the body,
query string or `X-Guest-Id` header.

### REQ-008: Availability reads require membership
WHEN a guest who is not a participant of the event reads its availabilities or a
participant's availability THEN THE SYSTEM SHALL respond `403 forbidden`
(`404 event_not_found` still wins for an unknown event).

### REQ-009: Public event lookup
WHEN anyone calls `GET /events/{publicId}` THEN THE SYSTEM SHALL keep returning
the event without authentication (the join screen needs it).

### REQ-010: Rate limiting
WHEN a client IP exceeds the configured per-minute limit on `POST /guests` or
`POST /events/{publicId}/join` THEN THE SYSTEM SHALL respond `429 rate_limited`.

## Scenarios

### SC-001: New guest
GIVEN no stored token WHEN the client calls `POST /guests` THEN it receives a
token, and `GET /events` with that token returns an empty list.

### SC-002: Legacy browser migrates
GIVEN a browser whose old guest id organizes event E WHEN it calls
`POST /guests { legacyGuestId }` and then `GET /events` with the returned token
THEN E is listed with `isOrganizer = true` and the guest can delete E.

### SC-003: Legacy id hijack attempt
GIVEN a legacy id already adopted WHEN another client tries to adopt it THEN it
gets `409 guest_already_claimed`.

### SC-004: Non-organizer cannot delete
GIVEN guest B joined E organized by guest A WHEN B deletes E THEN `403 forbidden`.

### SC-005: Outsider cannot read availability
GIVEN guest C who has not joined E WHEN C reads E's availabilities THEN
`403 forbidden`; after C joins, the same call returns `200`.

### SC-006: Missing token
GIVEN no `Authorization` header WHEN calling `GET /events` THEN `401 unauthorized`.

## Acceptance Criteria

- [ ] `POST /guests` returns a token; the database stores only its hash.
- [ ] Legacy adoption succeeds once, then returns 409; unknown ids get a fresh guest.
- [ ] No endpoint accepts `X-Guest-Id`, body `guestId` or query `guestId`.
- [ ] Missing/invalid token → 401 on every authenticated endpoint.
- [ ] Non-participants get 403 on both availability reads.
- [ ] `GET /events/{publicId}` remains public.
- [ ] Join and guest creation are rate-limited per IP (429).
- [ ] OpenAPI document declares the `Guest` security scheme.

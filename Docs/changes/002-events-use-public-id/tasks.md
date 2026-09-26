# Events use public id: Tasks

Landed: 2026-09-24T23:38:02Z

Ordered. Each task depends on the ones above it.

## Task 1 — Update application exceptions

**Depends on:** none

**Goal:** Exceptions report the public id, not the internal UUID.

**Work:**

- `InvalidPasscodeException`, `ParticipantAlreadyJoinedException`,
  `OrganizerCannotLeaveException`: change `Guid eventId` → `string publicId`.
- `EventNotFoundException`: remove the `Guid` overload, keep `string publicId`.

**Acceptance criteria:**

- [x] Exceptions compile and carry the public id in their message.

**Verification:**

- `dotnet build`

Verified: 2026-09-24T23:38:02Z

## Task 2 — Update use cases

**Depends on:** Task 1

**Goal:** Join/leave resolve by public id; create stops returning internal id.

**Work:**

- `JoinEvent.Execute(string publicId, ...)` uses `GetByPublicId`.
- `LeaveEvent.Execute(string publicId, ...)` uses `GetByPublicId`.
- `CreateEvent.Execute` drops `EventId` from its response.

**Acceptance criteria:**

- [x] Join/leave find events by public id and throw the string-publicId exceptions.

**Verification:**

- `dotnet build`

Verified: 2026-09-24T23:38:02Z

## Task 3 — Update API layer

**Depends on:** Task 2

**Goal:** Endpoints and DTOs use public id.

**Work:**

- `CreateEventResponse` drops `EventId`.
- `EventsRestService`: join/leave routes use `{publicId}` with `string publicId`;
  join `Location` → `/events/{publicId}/participants/{participantId}`.

**Acceptance criteria:**

- [x] Routes accept public id; create response has no `eventId`.

**Verification:**

- `dotnet build`

Verified: 2026-09-24T23:38:02Z

## Task 4 — Update tests

**Depends on:** Task 3

**Goal:** Tests use the public id for join/leave/fetch.

**Work:**

- Create/passcode-persistence tests fetch via `GetByPublicId(response.PublicId)`.
- Join/leave/search/availability tests pass `created.PublicId` to join/leave.
- Unknown-event cases use a non-existent public id string.

**Acceptance criteria:**

- [x] All tests pass.

**Verification:**

- `dotnet test`

Verified: 2026-09-24T23:38:02Z

## Task 5 — Fold deltas into feature specs

**Depends on:** Task 4

**Goal:** Feature specs reflect the public-id-only API.

**Work:**

- Update `Docs/specs/events/{spec,design,requirements}.md`.

**Acceptance criteria:**

- [x] Specs no longer describe join/leave by internal id or create returning eventId.

**Verification:**

- Manual review.

Verified: 2026-09-24T23:38:02Z

# 005-event-deletion: Verification

**Status:** PASS

## Summary

Both tasks of change 005-event-deletion were verified independently. The implementation adds a `DELETE /events/{publicId}` endpoint that hard-deletes an event and, via the existing cascade foreign keys, removes its participants and their availability ranges. The organizer-only guard, error codes, and request-shape validation match the existing `PUT /events/{publicId}/params` pattern. All 96 unit tests and 83 integration tests pass, the 7 `DeleteEventTests` cases pass, and the `events.hurl` workflow passes 11/11 live requests against the running API. No migration was added, no untouched files were modified, and no out-of-scope behavior crept in.

## Task results

- **Task 1 — `DeleteEvent` use case, `EventRepository.Delete`, `DELETE /events/{publicId}`, DI + integration tests:** PASS
  - `DeleteEvent.cs` exists with the correct load → guard → delete orchestration.
  - `EventRepository.Delete(Event)` removes the aggregate and commits in one method.
  - `EventsRestService.cs` maps `DELETE /events/{publicId}` with inline `400` validation before `DeleteEvent.Execute`.
  - `DeleteEvent` is registered `Scoped` in both production DI and the integration test fixture.
  - `DeleteEventTests.cs` contains all 7 required cases and they pass (7/7).
  - No migration, no domain changes, no `ExceptionHandler` changes, no availability code changes.

- **Task 2 — `events.hurl` wire entries + TypeScript client regen:** PASS
  - `events.hurl` lists entries 1–11 correctly and contains three `400` request-shape entries, one organizer `204` delete, and a final `GET` asserting `404`.
  - Live `hurl --test` run succeeded: 11 requests, 11 successes.
  - The regenerated TypeScript client exposes `deleteEvent` with `DeleteEventData` / `DeleteEventResponses` for `url: '/events/{publicId}'` and the expected 204/400/403/404 responses.

## Acceptance criteria

- [x] `DELETE /events/{publicId}` exists and deletes the event identified by the shareable public id. — Verified by `EventsRestService.cs:130` and `DeleteEventTests`.
- [x] The caller is identified solely by the `X-Guest-Id` header; no request body is accepted. — Verified by route signature (`HttpContext httpContext`, no request DTO) and TS client `DeleteEventData.body?: never`.
- [x] A successful delete returns `204` with no body and no response record. — Verified by `Results.NoContent()` at `EventsRestService.cs:143` and live hurl entry 10.
- [x] A missing, blank, or malformed `X-Guest-Id` header returns `400` before any lookup. — Verified by inline `Results.BadRequest` at `EventsRestService.cs:135-139` and hurl entries 7–9.
- [x] A well-formed non-organizer `X-Guest-Id` (other participant or stranger) returns `403 forbidden`. — Verified by `DeleteEventTests.Execute_ShouldThrowForNonOrganizer` and `Execute_ShouldThrowForStranger`.
- [x] A missing event returns `404 event_not_found`. — Verified by `DeleteEventTests.Execute_ShouldThrowForUnknownEvent`.
- [x] A second delete of the same public id returns `404 event_not_found` (not idempotent). — Verified by `DeleteEventTests.Execute_ShouldThrowForAlreadyDeletedEvent`.
- [x] Deletion removes the event, every participant (regardless of how many remain joined), and every participant's availability ranges. — Verified by `Execute_ShouldDeleteEventWithParticipants` and `Execute_ShouldDeleteAvailabilitiesWithParticipants`.
- [x] Deletion is hard: nothing remains, nothing is recoverable, and no record is kept. — Verified by `EventRepository.Delete` using `Remove` + `SaveChangesAsync`; no soft-delete fields or audit tables exist.
- [x] A passcode-protected event deletes identically with no passcode required. — Verified by `Execute_ShouldDeletePasscodeProtectedEvent`; `DeleteEvent` never reads `PasscodeHash`/`IsPasscodeProtected`.
- [x] After deletion, `GET /events/{publicId}` returns `404 event_not_found`. — Verified by hurl entry 11 and existing read-side behavior.
- [x] After deletion, `GET /events/{publicId}/participant` returns `404 event_not_found`. — Verified by existing `GetMyParticipant` behavior; no code changes needed.
- [x] After deletion, both availability `QUERY` reads return `404 event_not_found`. — Verified by existing availability read behavior; no code changes needed.
- [x] After deletion, `GET /events?guestId=` no longer lists the event for any former participant. — Verified by existing `search_events.sql` behavior; no code changes needed.
- [x] No schema change / no migration is required. — Verified by `git status --short src/FindAtime.Api/Migrations/` returning empty.
- [x] The params, join, and leave contracts are unchanged. — Verified by `git diff --stat` showing no changes to those routes and by full test suite passing.
- [x] No new error codes or exception types are introduced. — Verified by unchanged `ExceptionHandler.cs` mappings; only `event_not_found` and `forbidden` are returned.
- [x] Availability behavior is otherwise unchanged (the only change is cascade removal on delete). — Verified by `git diff --stat src/FindAtime.Api/Features/Availability/` returning empty and `DeleteEvent`/`EventRepository` not referencing `AvailabilityRepository`.
- [x] `events.hurl` ends with a delete entry followed by a `GET /events/{publicId}` asserting `404`. — Verified by file inspection and live hurl run.
- [x] The regenerated TypeScript client exposes a `DELETE /events/{publicId}` method. — Verified by `sdk.gen.d.ts` and `types.gen.d.ts`.
- [x] Integration tests prove the availability cascade (participants and their availability are gone), organizer success with participants present, non-organizer `403`, missing `404`, and double-delete `404`; the request-shape `400`s are asserted in `events.hurl`. — Verified by `DeleteEventTests` (7/7) and hurl entries 7–9.

## Spec conformance

| Req | How verified |
|-----|--------------|
| REQ-001 | `EventsRestService.cs:130` `MapDelete("/events/{publicId}", …)` |
| REQ-002 | Route binds only `publicId`, `HttpContext`, and `DeleteEvent`; no request DTO; TS `DeleteEventData.body?: never` |
| REQ-003 | `EventsRestService.cs:143` `return Results.NoContent();` and `.Produces(204)`; hurl entry 10 |
| REQ-004 | Inline `BadRequest` at `EventsRestService.cs:135-139` runs before `DeleteEvent.Execute`; hurl entries 7–9 |
| REQ-005 | `DeleteEvent.cs:16-19` throws `ForbiddenException` for any non-organizer guest id; tests cover participant and stranger |
| REQ-006 | `DeleteEvent.cs:12-14` throws `EventNotFoundException` for missing/already-deleted event; `Execute_ShouldThrowForAlreadyDeletedEvent` proves non-idempotency |
| REQ-007 | `Execute_ShouldDeleteEventWithParticipants` asserts `GetByPublicId` returns `null` |
| REQ-008 | Same test deletes an event that still has a joined friend; passes |
| REQ-009 | `Execute_ShouldDeleteAvailabilitiesWithParticipants` asserts `DbContext.Availabilities` has no row for the friend's `ParticipantId` |
| REQ-010 | `EventRepository.Delete` uses EF `Remove` + `SaveChangesAsync`; no soft-delete fields or audit tables |
| REQ-011 | `Execute_ShouldDeletePasscodeProtectedEvent` deletes with no passcode; `DeleteEvent` never reads passcode fields |
| REQ-012 | Existing read behavior; hurl entry 11 asserts 404 |
| REQ-013 | Existing read behavior (`GetMyParticipant` throws `EventNotFoundException` when event missing) |
| REQ-014 | Existing read behavior (availability reads throw `EventNotFoundException` when event missing) |
| REQ-015 | Existing read behavior (`search_events.sql` no longer joins deleted event) |
| REQ-016 | `git status --short src/FindAtime.Api/Migrations/` empty |
| REQ-017 | `git diff --stat` on params/join/leave routes empty; full suite passes |
| REQ-018 | `ExceptionHandler.cs` unchanged; only `event_not_found`/`forbidden` returned |
| REQ-019 | No availability source changes; `DeleteEvent`/`EventRepository` do not reference availability code |
| REQ-020 | `events.hurl` entries 7–11 match spec; final entry is `GET … 404` |
| REQ-021 | `sdk.gen.d.ts:18` and `types.gen.d.ts:119-147` expose `deleteEvent` |
| REQ-022 | `DeleteEventTests` proves cascade, organizer success, 403, 404, double-delete 404; hurl entries 7–9 prove 400s at wire level |

## Design conformance

- **Use case shape:** `DeleteEvent` follows the `UpdateEventParams` load → find-organizer → compare `GuestId` → act pattern, using the shared `ForbiddenException` from `Features/Availability/Application/Exceptions`.
- **Repository seam:** `EventRepository.Delete(Event)` removes and commits in one method, matching the `Save` method's "persist within repository" shape. No direct `DbContext` access from the use case and no `AvailabilityRepository` call.
- **Domain:** `Event.cs` is untouched; no `Event.Delete()` marker was added.
- **API:** `MapDelete` copies the `X-Guest-Id` parsing block verbatim from `MapPut` for params; `Produces` metadata lists 204/400/403/404.
- **DI:** `DeleteEvent` registered `Scoped` in both production and test fixtures, placed immediately after `UpdateEventParams`.
- **Persistence:** No migration; delete relies on the committed cascade FKs (`events→participants` and `participants→availabilities`). Server logs confirmed EF emits `DELETE FROM participants` followed by `DELETE FROM events` inside one implicit transaction.
- **Testing:** Seven integration cases cover the required behaviors; request-shape 400s are verified in `events.hurl` as resolved in "Resolved clarifications".
- **Hurl:** Entries 7–9 are the request-shape 400s, entry 10 is the organizer 204 delete, entry 11 is the post-delete 404 GET; nothing id-dependent follows the final GET.
- **TypeScript client:** Regenerated client contains the new `deleteEvent` operation and keeps all existing operations.

## Issues found

None that affect correctness or conformance.

**Caveats noted (not failures):**

1. **`typescript-client/dist/` is gitignored.** The regenerated files exist on disk and expose the required `deleteEvent` method, but they do not appear in `git status` because `.gitignore:42` ignores `typescript-client/dist/`. This is a pre-existing repository convention, not a defect introduced by this change.
2. **Proposal phantom claim.** `proposal.md` states that the two availability `QUERY` reads return `200` with an empty body for a missing event. This was investigated during spec/design and corrected: the code throws `EventNotFoundException` first in those read paths, so the actual behavior is `404`. The spec and design now record the corrected truth; `proposal.md` is the historical record and was superseded by a user-approved clarification. No implementation change is required.

## Command outputs (trimmed)

```
$ PATH=/home/ar/.dotnet:$PATH DOTNET_ROOT=/home/ar/.dotnet dotnet build
Build succeeded. 0 Warning(s), 0 Error(s)

$ PATH=/home/ar/.dotnet:$PATH DOTNET_ROOT=/home/ar/.dotnet dotnet test tests/FindAtime.IntegrationTests/FindAtime.IntegrationTests.csproj --filter "DeleteEventTests"
Passed! Failed: 0, Passed: 7, Skipped: 0, Total: 7

$ PATH=/home/ar/.dotnet:$PATH DOTNET_ROOT=/home/ar/.dotnet dotnet test
Passed! Failed: 0, Passed: 96, Skipped: 0, Total: 96  (FindAtime.UnitTests)
Passed! Failed: 0, Passed: 83, Skipped: 0, Total: 83  (FindAtime.IntegrationTests)

$ hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl
Success (11 request(s) in 1251 ms)
Executed files: 1, Succeeded files: 1 (100.0%), Failed files: 0

$ git status --short src/FindAtime.Api/Migrations/
(empty)

$ git diff --stat src/FindAtime.Api/Common/ExceptionHandler.cs src/FindAtime.Api/Features/Availability/ src/FindAtime.Api/Features/Events/Domain/ src/FindAtime.Api/Features/Availability/Api/availability.hurl
(empty)
```

## Consistency and phantom adjudication

- **REQ-022 / 400 verification location:** `spec.md`, `design.md` ("Resolved clarifications"), and `tasks.md` (Sequencing notes + Task 1/2 acceptance criteria) all now agree: the request-shape `400`s for missing/blank/malformed `X-Guest-Id` are verified at the wire level in `events.hurl`, while `DeleteEventTests` covers the seven use-case behaviors. The acceptance-criteria boxes in `tasks.md` and the spec acceptance criteria reflect this split. Story is consistent.
- **Proposal's 200-empty claim:** Treated as a historical artifact. The claim was investigated, disproven, and corrected in the spec/design with user approval. The implementation correctly returns `404` for post-deletion availability reads because the existing read paths throw `EventNotFoundException` first. No fix is needed.

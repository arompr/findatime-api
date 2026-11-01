# 005-event-deletion: Design

## Summary

Give the organizer a way to end their own event: a new **`DELETE
/events/{publicId}`** endpoint that **hard-deletes** the event and, via the two
cascade foreign keys already in the schema, every participant and every
participant's availability ranges. The caller is identified **solely** by the
`X-Guest-Id` header (no request body), exactly as the existing organizer-only
`PUT /events/{publicId}/params` command does.

The change is additive and code-only:

- **Application (events)** — a new `DeleteEvent` use case that loads the event,
  throws `EventNotFoundException` (404) if it is missing, resolves the organizer
  and throws `ForbiddenException` (403) on a guest-id mismatch, then deletes.
- **Persistence** — a new `EventRepository.Delete(Event)` method (EF `Remove` +
  `SaveChangesAsync`). **No migration**: both cascade FKs already exist in DDL.
- **Domain** — **no change**. Deletion is a persistence/use-case concern, not a
  domain rule; the organizer guard is implemented in the use case exactly like
  `UpdateEventParams`. No marker method is added to `Event`.
- **API** — the `MapDelete` route in `EventsRestService`, copying the
  `X-Guest-Id` parsing and inline `Results.BadRequest` from the params endpoint,
  returning `Results.NoContent()` (204). `Common/ExceptionHandler` already maps
  `EventNotFoundException` → 404 `event_not_found` and `ForbiddenException` →
  403 `forbidden`; **zero changes**.
- **DI** — register `DeleteEvent` (`Scoped`) in `Composition/ServiceCollectionExtensions.cs`
  and `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs`.
- **Tests** — new `DeleteEventTests` (integration) proving the cascade and every
  use-case failure mode; the three request-shape 400s are wire-verified via
  `events.hurl` (see "Resolved clarifications").
- **Wire + client** — `events.hurl` gains the delete entry (last mutating entry)
  plus a post-delete `GET` asserting 404, and the TypeScript client is
  regenerated via `make typescript-client`.

No change to `params`/`join`/`leave` contracts, no new error codes or exception
types, no availability code changes (the cascade does the work), no soft-delete,
no passcode gate on deletion.

## Resolved clarifications

**Request-shape 400 verification (supersedes the literal wording of REQ-022).**
REQ-022 says the *integration test suite* SHALL prove the missing/blank/malformed
`X-Guest-Id` → `400` cases, and the fixed decisions bundled that into
`DeleteEventTests`. But the 400s are inline `Results.BadRequest` in the new
endpoint, and this repo has **no HTTP-level test infrastructure**
(`DeleteEvent.Execute(string, Guid)` receives an already-parsed `Guid`, so it can
never observe a missing/blank/malformed header). Per the user's resolution
(**Option A**), the 400s are verified **at the wire level in `events.hurl`**, not
in `DeleteEventTests`. `DeleteEventTests` covers the seven use-case behaviors
below; the hurl file carries the three request-shape 400 cases. The orchestrator
will align the spec's REQ-022 wording before verify so the artifacts stay
consistent.

## Use case: `DeleteEvent`

New file `src/FindAtime.Api/Features/Events/Application/DeleteEvent.cs`.

- Constructor: `DeleteEvent(EventRepository eventRepository)` — a single
  dependency. No `TimezoneConverter` (nothing is converted) and no factory.
- Signature: `public async Task Execute(string publicId, Guid guestId)` —
  returns `Task` (no result), matching `LeaveEvent`. The endpoint already parsed
  `X-Guest-Id` into a `Guid`.
- Operation order (deterministic; mirrors `UpdateEventParams` for the guard):

  1. `Event? domainEvent = await this._eventRepository.GetByPublicId(publicId);`
     `GetByPublicId` loads the aggregate with `Include(e => e.Participants)`.
     If `null` → `throw new EventNotFoundException(publicId);` (404).
  2. `Participant? organizer = domainEvent.FindParticipant(domainEvent.OrganizerParticipantId);`
  3. `GuestId requestGuestId = GuestId.FromString(guestId.ToString());`
     `if (organizer is null || organizer.GuestId != requestGuestId)`
     → `throw new ForbiddenException($"Guest id '{guestId}' is not the organizer of event '{publicId}'.");`
     (403). The `organizer is null` branch is defensive — the organizer is
     always a participant, but it keeps the shape identical to
     `UpdateEventParams`. A stranger and a non-organizer participant both fail
     here the same way (their `GuestId` simply doesn't match).
  4. `await this._eventRepository.Delete(domainEvent);` — the delete + persist.

Passcode-protected events need nothing special: the use case never reads
`PasscodeHash`/`IsPasscodeProtected`, so a protected event deletes exactly like
an unprotected one (REQ-011).

### Repository seam decision

The delete goes through **`EventRepository`, not `DbContext` directly**. Every
use case in this codebase depends only on repositories/factories/converters and
never touches `DbContext`; `DbContext` access is confined to the Infra layer
(`EventRepository`, `AvailabilityRepository`). Introducing direct
`DbContext.Events.Remove(...)` in `DeleteEvent` would break that invariant. The
precedent is `AvailabilityRepository.DeleteForParticipant` (a delete op living
on the repository) and `EventRepository.Save` (which already wraps
`SaveChangesAsync`).

Unlike `AvailabilityRepository.DeleteForParticipant` (which *stages* a
`RemoveRange` and lets a later `Save` commit, because `SetAvailability` needs a
delete-then-add to be atomic), `DeleteEvent` has nothing to add afterward, so
`Delete` removes **and** commits in one method — the same "persist within the
repository method" shape as `EventRepository.Save`.

## Persistence

New method on `src/FindAtime.Api/Features/Events/Infra/EventRepository.cs`
(no `using` change needed — `global using Microsoft.EntityFrameworkCore;` in
`GlobalUsings.cs` already provides `EntityState`/`Remove`/`SaveChangesAsync`):

```
public async Task Delete(Event domainEvent)
{
    this._dbContext.Events.Remove(domainEvent);
    await this._dbContext.SaveChangesAsync();
}
```

### Why no migration (REQ-016)

Both cascade foreign keys already exist in the committed migrations and the
model snapshot; nothing about the schema changes:

- `events → participants`: migration `20260908005715_PersistDomainEntities`
  adds `FK_participants_events_event_id` with `onDelete: ReferentialAction.Cascade`.
- `participants → availabilities`: migration `20260920000223_AddAvailabilities`
  adds `FK_availabilities_participants_participant_id` with
  `onDelete: ReferentialAction.Cascade`.

`FindatimeDbContextModelSnapshot.cs` models both with `DeleteBehavior.Cascade`
(lines ~153–156 for `Availability → Participant`, ~162–166 for
`Participant → Event`). **No `dotnet ef migrations add` is run for this change.**

### What EF issues on `Remove(event)` and why the DB cascade covers availabilities

`GetByPublicId` loads the event *with* its participants, so both the `Event` and
its `Participant` entities are change-tracked. `Remove(event)` marks the event
**and** each tracked participant `Deleted`; `SaveChangesAsync()` emits DELETE
statements in dependency order — the participant DELETEs **before** the event
DELETE, so there is no FK violation. The `Availability` rows are **not** loaded
or tracked (the aggregate doesn't own them), so EF issues nothing for them; the
database's `ON DELETE CASCADE` on `participants.id → availabilities.participant_id`
removes them automatically when the participant rows are deleted.

The redundant participant DELETEs are the accepted cost of the consistency-first
choice (fixed Q4): participant counts are small (tens), and keeping the write
style uniform (EF for writes, Dapper for reads) is worth a few extra statements.

### Transactionality

A single `SaveChangesAsync()` wraps the entire removal in one transaction (the
`DbContext`'s implicit transaction), so the event, its participants, and (via
the DB cascade) their availabilities are removed atomically.

## Domain

**No change.** `Event` keeps its aggregate shape; `Event.Create`,
`AddParticipant`, `RemoveParticipant`, `FindParticipant`, `SetParams` are
untouched. No `Event.Delete()` marker is added — deletion is a
persistence/orchestration concern, and the only rule involved (organizer-only)
is enforced in the use case exactly as `UpdateEventParams` does. This is the
minimal choice the proposal leaned toward.

## API

New route in `src/FindAtime.Api/Features/Events/Api/EventsRestService.cs`
(inside `MapEvents`, after the leave route):

```
app.MapDelete(
        "/events/{publicId}",
        async (string publicId, HttpContext httpContext, DeleteEvent deleteEvent) =>
        {
            string? guestId = httpContext.Request.Headers["X-Guest-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(guestId))
                return Results.BadRequest("X-Guest-Id header is required");

            if (!Guid.TryParse(guestId, out var parsedGuestId))
                return Results.BadRequest("X-Guest-Id header must be a valid uuid");

            await deleteEvent.Execute(publicId, parsedGuestId);

            return Results.NoContent();
        }
    )
    .WithName("DeleteEvent")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound);
```

Notes:

- The `X-Guest-Id` parsing block is **copied verbatim** from the
  `PUT /events/{publicId}/params` endpoint (`EventsRestService.cs` lines 67–72)
  — `string.IsNullOrWhiteSpace` → 400 "X-Guest-Id header is required";
  `Guid.TryParse` → 400 "X-Guest-Id header must be a valid uuid". This is the
  one and only "request-shape before any lookup" path (REQ-004): a missing,
  blank, or malformed header is rejected inline before `Execute` runs, so no
  event lookup occurs.
- There is **no request body** and no request DTO (REQ-002); the route only
  binds `publicId` from the route, `HttpContext` for the header, and the
  injected `DeleteEvent`.
- Success is `Results.NoContent()` (REQ-003) — the same 204 shape as
  `POST /events/{publicId}/leave`. No response record is added.
- `Produces` metadata lists `204`/`400`/`403`/`404`, mirroring the params
  endpoint's `Produces` list. (OpenAPI auto-documents the route; the two `QUERY`
  availability routes remain absent from the generated client as a pre-existing
  limitation, unrelated to this route.)

### Exception handling — zero changes

`Common/ExceptionHandler.cs` already maps:

- `EventNotFoundException` → `404` `event_not_found` (missing and
  already-deleted, REQ-006 — deletion is **not** idempotent, a second delete
  404s like every other missing-event command).
- `ForbiddenException` → `403` `forbidden` (non-organizer participant or
  stranger, REQ-005).

No new exception type, no new dictionary entry, no `if` chain. Note
`ForbiddenException` is defined at
`src/FindAtime.Api/Features/Availability/Application/Exceptions/ForbiddenException.cs`
(shared across features), which is fine — `DeleteEvent` references the type in
scope, exactly as `UpdateEventParams` and `SetAvailability` already do.

## Post-deletion read behavior (unchanged, no code)

The spec records existing behavior accurately; the proposal's "200-empty
inconsistency" was a phantom and this design does not reintroduce it:

- `GET /events/{publicId}` → 404 (existing `ReadEventService.GetEventByPublicId`
  returns null → `EventNotFoundException`).
- `GET /events/{publicId}/participant` → 404 (`GetMyParticipant` throws
  `EventNotFoundException` when `GetEventByPublicId` returns null).
- `QUERY /events/{publicId}/availabilities` → 404 (`GetEventAvailabilities`
  throws `EventNotFoundException` when `ReadAvailabilityService.GetAllForEvent`
  returns null).
- `QUERY /events/{publicId}/participants/{participantId}/availability` → 404
  (`GetParticipantAvailability` throws `EventNotFoundException` first).
- `GET /events?guestId=` → the event is gone from `search_events.sql` results.

No read-side file is modified in this change.

## DI

- `src/FindAtime.Api/Composition/ServiceCollectionExtensions.cs` — add
  `services.AddScoped<DeleteEvent>();` alongside the other events use cases
  (`CreateEvent`, `JoinEvent`, `LeaveEvent`, `UpdateEventParams`).
- `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` — add
  `services.AddScoped<DeleteEvent>();` in the same block so the new test class
  can resolve it.

## Testing

### Integration tests — `tests/FindAtime.IntegrationTests/Application/Events/DeleteEventTests.cs` (new)

Follows the `LeaveEventTests`/`UpdateEventParamsTests` command-test pattern
(`[Collection(PostgresCollection.Name)]`, `IntegrationTest` base, resolve use
cases in `InitializeAsync`, seed via `CreateEvent`/`JoinEvent`/`SetAvailability`,
assert via `EventRepository.GetByPublicId` and `DbContext.Availabilities`).

Fields resolved in `InitializeAsync`: `CreateEvent`, `JoinEvent`,
`SetAvailability`, `DeleteEvent`, `EventRepository`. (`SetAvailability` is
needed only for the cascade case.) Use `TestEvents.OrganizerGuestId` /
`TestEvents.Name` / `TestEvents.OrganizerName` and a local `FriendGuestId`
(e.g. `"22222222-2222-2222-2222-222222222222"`), as the sibling test classes do.

| Test | Seeds | Asserts | Covers |
|------|-------|---------|--------|
| `Execute_ShouldDeleteEventWithParticipants` | `CreateEvent` (organizer) + `JoinEvent` (friend) | `DeleteEvent.Execute(publicId, Guid.Parse(OrganizerGuestId))` succeeds; `_repository.GetByPublicId(publicId)` returns `null` | REQ-007, REQ-008; SC-001 |
| `Execute_ShouldDeleteAvailabilitiesWithParticipants` | `CreateEvent` + `JoinEvent` (friend, capture `ParticipantId`) + `SetAvailability` for the friend | after delete, `_repository.GetByPublicId(publicId)` is `null` **and** `DbContext.Availabilities` has no row where `ParticipantId == ParticipantId.FromString(friendParticipantId)` | REQ-009; SC-010 (the cascade proof) |
| `Execute_ShouldThrowForNonOrganizer` | `CreateEvent` + `JoinEvent` (friend) | `DeleteEvent.Execute(publicId, Guid.Parse(FriendGuestId))` throws `ForbiddenException`; event still exists (non-null) | REQ-005; SC-002 |
| `Execute_ShouldThrowForStranger` | `CreateEvent` | `DeleteEvent.Execute(publicId, Guid.Parse("99999999-9999-9999-9999-999999999999"))` throws `ForbiddenException`; event still exists | REQ-005; SC-003 |
| `Execute_ShouldThrowForUnknownEvent` | — | `DeleteEvent.Execute("nonexistent1", Guid.Parse(OrganizerGuestId))` throws `EventNotFoundException` | REQ-006; SC-004 |
| `Execute_ShouldThrowForAlreadyDeletedEvent` | `CreateEvent` | first `Execute` succeeds; second `Execute` throws `EventNotFoundException` | REQ-006; SC-005 (non-idempotent) |
| `Execute_ShouldDeletePasscodeProtectedEvent` | `CreateEvent(..., isPasscodeProtected: true)` | `Execute(publicId, Guid.Parse(OrganizerGuestId))` succeeds with no passcode; `_repository.GetByPublicId(publicId)` is `null` | REQ-011; SC-007 |

The cascade assertion (second row) is the one that proves the
`participants → availabilities` FK is actually in force at runtime, not just
that the event row vanished — a regression that deleted only the event (or broke
the cascade) would otherwise pass silently.

### Request-shape 400s

**Not** in `DeleteEventTests` — verified in `events.hurl` (see below), per the
"Resolved clarifications" note.

### Unit tests

**None.** `DeleteEvent` is pure orchestration (load → guard → delete) with no
domain logic of its own; the organizer guard is already exercised by
`UpdateEventParamsTests`. Adding a unit test would mock `EventRepository` and
restate the integration coverage without adding signal.

## Hurl scratch file — `src/FindAtime.Api/Features/Events/Api/events.hurl`

Three request-shape 400 entries precede the delete; the delete remains the last
mutating entry, followed by the post-delete 404 GET (REQ-020). Reuse the
captured `publicId` (from entry 1) and the static `organizerGuestId`.

New entries (append after entry 6, renumbering the list):

```
# Delete with a missing X-Guest-Id header (request-shape 400)
DELETE {{baseUrl}}/events/{{publicId}}

HTTP 400

# Delete with a blank X-Guest-Id header (request-shape 400)
DELETE {{baseUrl}}/events/{{publicId}}
X-Guest-Id:

HTTP 400

# Delete with a malformed X-Guest-Id header (request-shape 400)
DELETE {{baseUrl}}/events/{{publicId}}
X-Guest-Id: not-a-uuid

HTTP 400

# Delete the event (organizer only)
DELETE {{baseUrl}}/events/{{publicId}}
X-Guest-Id: {{organizerGuestId}}

HTTP 204

# Assert the event is gone
GET {{baseUrl}}/events/{{publicId}}

HTTP 404
```

The 400 entries use the captured `{{publicId}}` because the endpoint rejects
before any lookup — the event still exists at that point, but that is irrelevant
to the assertion. The success DELETE (204) must come after the 400 entries and
after every entry that needs the event to exist; it is the last mutating entry,
and nothing after it depends on the event. The final GET asserts 404, the
wire-level proof that deletion took effect (the cascade itself is proven only in
`DeleteEventTests`, since hurl cannot read the `availabilities` table).

Update the header entry list (lines 9–15) to:

```
# Entries:
#   1  POST   /events
#   2  GET    /events?guestId=
#   3  GET    /events/{publicId}
#   4  PUT    /events/{publicId}/params      (X-Guest-Id; timezone + intervalMinutes required)
#   5  POST   /events/{publicId}/join
#   6  POST   /events/{publicId}/leave
#   7  DELETE /events/{publicId}             (missing X-Guest-Id -> 400)
#   8  DELETE /events/{publicId}             (blank X-Guest-Id -> 400)
#   9  DELETE /events/{publicId}             (malformed X-Guest-Id -> 400)
#  10  DELETE /events/{publicId}             (X-Guest-Id; organizer only -> 204)
#  11  GET    /events/{publicId}             (assert 404 after delete)
```

## TypeScript client

Run `make typescript-client` (builds the API in Release, runs `npm run prepare`
in `typescript-client/`) after adding the route. The regenerated SDK gains a
method for `DELETE /events/{publicId}` (header `X-Guest-Id`, no body, 204/400/
403/404) and the OpenAPI models update. The two `QUERY` availability endpoints
remain absent from the generated client (pre-existing limitation). The frontend
gains the ability to call delete; backend and regenerated client ship in
lockstep, as with the `intervalMinutes` change in 004.

## Migration / Rollout

1. **No migration.** Both cascade FKs are already committed (see Persistence);
   `dotnet ef database update` is not needed for this change.
2. Implement + build + test: `dotnet build`, `dotnet test`.
3. Regenerate the client: `make typescript-client`.
4. Ship backend and TS client together (additive — an older client simply lacks
   the delete method; no breaking wire change to existing contracts).

## Task breakdown guidance (for `tasks.md`, authored by sdd-tasks)

Two tasks, ordered by dependency:

1. **Use case + repository + endpoint + DI + tests** —
   `Features/Events/Application/DeleteEvent.cs`,
   `Features/Events/Infra/EventRepository.cs` (`Delete`),
   `Features/Events/Api/EventsRestService.cs` (`MapDelete`),
   `Composition/ServiceCollectionExtensions.cs` +
   `tests/.../Fixtures/IntegrationTest.cs` registrations, and
   `tests/.../Application/Events/DeleteEventTests.cs`. *Depends on: nothing.*
2. **Hurl + TypeScript client** — `events.hurl` (three 400 entries + delete +
   post-delete 404 GET + header list update) and `make typescript-client`.
   *Depends on: 1.*

Each task carries a `dotnet build`/`dotnet test` self-check per the SDD
convention.

## Apply-bite risks

- **EF participant DELETEs run before the parent delete — no FK violation.**
  Because `GetByPublicId` includes participants, `Remove(event)` marks the
  tracked participants `Deleted` and EF orders their DELETEs before the event's.
  Do **not** "optimize" by loading the event without participants: that would
  also work (the DB cascade would then handle participants), but the
  organizer-check already requires the participants to be loaded, so keep
  `GetByPublicId` as-is. Do **not** add an `AvailabilityRepository` delete for
  this path — the DB cascade covers availabilities; an explicit availability
  delete would be dead weight and a second write style.
- **Second delete 404s (not idempotent).** `GetByPublicId` returns `null` after
  deletion, so the second `Execute` throws `EventNotFoundException` before the
  organizer check. No special-casing is needed — do not add one.
- **hurl ordering.** The 400 and delete entries must come after every
  id-dependent entry that needs the event alive. Placing the 400s before the
  success DELETE (and the success DELETE last, before the 404 GET) keeps every
  earlier entry working. The final GET is the only entry that must run after the
  delete; do not add further id-dependent entries after it.
- **Lockstep client regen.** The delete button needs the regenerated method;
  ship the client with the backend. There is no breaking change to existing
  client calls, so an older client is not broken by this change.
- **`ForbiddenException` location.** It is shared from
  `Features/Availability/Application/Exceptions/`; do not create a duplicate
  under `Features/Events/Application/Exceptions/`.

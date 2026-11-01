# 005-event-deletion: Tasks

Landed: 2026-11-01T19:27:54Z

Ordered. Each task depends on the ones above it unless marked `[P]`
(parallel-safe). Each task ends with a `Verified: <iso>` stamp once its
**Verification** has passed. A fully landed change additionally carries a
`Landed: <iso>` line directly under its H1.

Environment:

- The solution targets `net11.0`. The `dotnet` on PATH is SDK 10 and fails, so
  every build/test command below is prefixed with `DOTNET_ROOT=/home/ar/.dotnet`.
  If a tool shells out to `dotnet` itself (e.g. `make typescript-client`), the
  prefix must be on the outer command too; fall back to
  `PATH=/home/ar/.dotnet:$PATH` if `DOTNET_ROOT` alone is not picked up.
- Integration tests run against Testcontainers PostgreSQL and need a working
  Podman: `podman compose up -d` before the first `dotnet test`.
- `hurl` is installed. `make typescript-client` regenerates
  `typescript-client/dist/generated/**`.

Sequencing notes:

- Tests ship with the task that owns the behaviour. All seven `DeleteEventTests`
  cases land in Task 1 with the use case, repository method and endpoint they
  exercise; Task 2 owns the wire-level artifacts (hurl entries, TS client regen).
- Task 1 carries **no migration** and **no unit tests**. Both cascade foreign keys
  are already committed (`FK_participants_events_event_id`,
  `FK_availabilities_participants_participant_id`), the domain and
  `Common/ExceptionHandler` are untouched, and `DeleteEvent` is pure orchestration,
  so a unit test would only restate the integration coverage
  (design "Testing"). Do not run `dotnet ef migrations add` anywhere in this change.
- The request-shape `400`s are verified **in `events.hurl` only** (design "Resolved
  clarifications", spec REQ-022): `DeleteEventTests` cannot observe a
  missing/blank/malformed header because `DeleteEvent.Execute(string, Guid)`
  receives an already-parsed `Guid`.
- Task 2 is not `[P]` with Task 1: it edits the hurl file whose delete entry
  exercises the Task 1 route, and the client regen needs the route mapped.

## Task 1 — `DeleteEvent` use case, `EventRepository.Delete`, `DELETE /events/{publicId}`, DI + integration tests

**Depends on:** none

**Goal:** Give the organizer a way to hard-delete their own event, identified
solely by the `X-Guest-Id` header, with every participant and every participant's
availability removed by the cascade that already exists in the schema.

**Work:**
- Create `src/FindAtime.Api/Features/Events/Application/DeleteEvent.cs` —
  `public class DeleteEvent` with a single dependency
  (`private EventRepository _eventRepository;`, constructor
  `DeleteEvent(EventRepository eventRepository)`) and
  `public async Task Execute(string publicId, Guid guestId)`, in this exact order:
  1. `Event? domainEvent = await this._eventRepository.GetByPublicId(publicId);`
     → `if (domainEvent is null) throw new EventNotFoundException(publicId);`
  2. `Participant? organizer = domainEvent.FindParticipant(domainEvent.OrganizerParticipantId);`
  3. `GuestId requestGuestId = GuestId.FromString(guestId.ToString());`
     → `if (organizer is null || organizer.GuestId != requestGuestId) throw new ForbiddenException($"Guest id '{guestId}' is not the organizer of event '{publicId}'.");`
     (copy the guard shape from
     `src/FindAtime.Api/Features/Events/Application/UpdateEventParams.cs:18-21`;
     do **not** create a new exception type and do **not** duplicate
     `ForbiddenException` — it is shared from
     `src/FindAtime.Api/Features/Availability/Application/Exceptions/ForbiddenException.cs`).
  4. `await this._eventRepository.Delete(domainEvent);`
  No `TimezoneConverter`, no factory, no `IsPasscodeProtected`/`PasscodeHash` read
  (a passcode-protected event must delete identically).
- Edit `src/FindAtime.Api/Features/Events/Infra/EventRepository.cs` — add below
  `GetByPublicId` (after line 36):
  `public async Task Delete(Event domainEvent)` doing
  `this._dbContext.Events.Remove(domainEvent); await this._dbContext.SaveChangesAsync();`.
  No new `using` (`global using Microsoft.EntityFrameworkCore;` in
  `GlobalUsings.cs` covers `Remove`/`SaveChangesAsync`). Remove **and** commit in
  one method (same shape as `Save`); the `ON DELETE CASCADE` on
  `participants → availabilities` removes the availability rows — do **not** add
  an `AvailabilityRepository` call for this path.
- Edit `src/FindAtime.Api/Features/Events/Api/EventsRestService.cs` — inside
  `MapEvents`, after the leave route (currently ending line 128), add
  `app.MapDelete("/events/{publicId}", async (string publicId, HttpContext httpContext, DeleteEvent deleteEvent) => { … })`
  whose body copies the `X-Guest-Id` parsing block **verbatim** from the
  `PUT /events/{publicId}/params` endpoint (lines 67–72):
  `string? guestId = httpContext.Request.Headers["X-Guest-Id"].FirstOrDefault();`
  → `string.IsNullOrWhiteSpace(guestId)` ⇒ `Results.BadRequest("X-Guest-Id header is required")`
  → `!Guid.TryParse(guestId, out var parsedGuestId)` ⇒
  `Results.BadRequest("X-Guest-Id header must be a valid uuid")`; then
  `await deleteEvent.Execute(publicId, parsedGuestId); return Results.NoContent();`.
  No request body, no request DTO, no response record. Chain
  `.WithName("DeleteEvent").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);`
- Edit `src/FindAtime.Api/Composition/ServiceCollectionExtensions.cs` — add
  `services.AddScoped<DeleteEvent>();` immediately after
  `services.AddScoped<UpdateEventParams>();` (line 34).
- Edit `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` — add the
  same `services.AddScoped<DeleteEvent>();` in the use-case block (after
  `services.AddScoped<UpdateEventParams>();`, line 44), so the new test class can
  resolve it.
- Create `tests/FindAtime.IntegrationTests/Application/Events/DeleteEventTests.cs`
  — follow `tests/FindAtime.IntegrationTests/Application/Events/LeaveEventTests.cs`:
  `[Collection(PostgresCollection.Name)]`, `: IntegrationTest`, override
  `InitializeAsync` to resolve `CreateEvent`, `JoinEvent`, `SetAvailability`,
  `DeleteEvent` and `EventRepository` from `Scope.ServiceProvider`, use
  `TestEvents.Name` / `TestEvents.OrganizerGuestId` / `TestEvents.OrganizerName`
  and a local `private const string FriendGuestId = "22222222-2222-2222-2222-222222222222";`.
  Seed via `CreateEvent.Execute(name, organizerGuestId, organizerName, isPasscodeProtected)`
  and `JoinEvent.Execute(publicId, passcode, FriendGuestId, "Bob", "UTC")`; capture
  the joined `ParticipantId` where needed; submit availability via
  `SetAvailability.Execute(publicId, Guid.Parse(participantId), Guid.Parse(FriendGuestId), "UTC", [new AvailabilityRangeResult(start, end)])`.
  Implement exactly the seven cases from design's Testing table:
  - `Execute_ShouldDeleteEventWithParticipants` — organizer + one friend joined;
    `Execute(publicId, Guid.Parse(TestEvents.OrganizerGuestId))` succeeds and
    `_repository.GetByPublicId(publicId)` is `null`.
  - `Execute_ShouldDeleteAvailabilitiesWithParticipants` — organizer + friend
    joined (capture `ParticipantId`) + availability submitted for the friend;
    after delete `_repository.GetByPublicId(publicId)` is `null` **and**
    `DbContext.Availabilities` contains no row with
    `ParticipantId == ParticipantId.FromString(friendParticipantId)` (the cascade
    proof — read `DbContext` via `Scope.ServiceProvider.GetRequiredService<DbContext>()`,
    `using Microsoft.EntityFrameworkCore;`).
  - `Execute_ShouldThrowForNonOrganizer` — friend joined;
    `Execute(publicId, Guid.Parse(FriendGuestId))` throws `ForbiddenException` and
    the event still exists (`GetByPublicId` non-null).
  - `Execute_ShouldThrowForStranger` — organizer-created event only;
    `Execute(publicId, Guid.Parse("99999999-9999-9999-9999-999999999999"))` throws
    `ForbiddenException` and the event still exists.
  - `Execute_ShouldThrowForUnknownEvent` —
    `Execute("nonexistent1", Guid.Parse(TestEvents.OrganizerGuestId))` throws
    `EventNotFoundException`.
  - `Execute_ShouldThrowForAlreadyDeletedEvent` — first `Execute` succeeds, second
    `Execute` throws `EventNotFoundException` (non-idempotent).
  - `Execute_ShouldDeletePasscodeProtectedEvent` — `CreateEvent.Execute(..., true)`;
    `Execute(publicId, Guid.Parse(TestEvents.OrganizerGuestId))` succeeds with no
    passcode and `GetByPublicId(publicId)` is `null`.
  Do **not** add request-shape/HTTP tests here: the 400s live in `events.hurl`.
- Explicitly out of scope for this task (do not create/edit):
  `src/FindAtime.Api/Features/Events/Domain/**` (no `Event.Delete()` marker),
  `src/FindAtime.Api/Common/ExceptionHandler.cs` (both mappings already exist),
  any new file under `src/FindAtime.Api/Migrations/`,
  `src/FindAtime.Api/Features/Availability/**` and its read SQL.

**Acceptance criteria:**
- [x] `DELETE /events/{publicId}` is mapped in `EventsRestService` as
  `MapDelete` named `DeleteEvent`, binds `publicId` from the route and identifies
  the caller only from the `X-Guest-Id` header — no request body or request DTO
  (REQ-001, REQ-002).
- [x] A successful delete returns `204` with no body and no response record
  (`Results.NoContent()` plus `Produces(204)`) (REQ-003).
- [x] A missing or blank `X-Guest-Id` returns the inline 400
  `"X-Guest-Id header is required"`, and a non-UUID header the inline 400
  `"X-Guest-Id header must be a valid uuid"`, both before `DeleteEvent.Execute`
  runs, so no event lookup happens (REQ-004, REQ-022 — asserted at the wire level
  in Task 2).
- [x] A well-formed guest id that is not the organizer — another participant or a
  stranger — throws `ForbiddenException` and leaves the event intact, surfacing as
  403 `forbidden` through the unchanged `ExceptionHandler` (REQ-005).
- [x] A missing event and a second delete of an already-deleted event both throw
  `EventNotFoundException` / 404 `event_not_found`; the second delete never
  returns 204 (REQ-006).
- [x] `DeleteEventTests.Execute_ShouldDeleteEventWithParticipants` proves the
  organizer deletes an event that still has a joined participant and
  `GetByPublicId` then returns `null` (REQ-007, REQ-008; SC-001).
- [x] `DeleteEventTests.Execute_ShouldDeleteAvailabilitiesWithParticipants` proves
  the cascade at runtime: after the delete no `Availabilities` row exists for the
  friend's `ParticipantId` (REQ-009; SC-010).
- [x] `DeleteEventTests.Execute_ShouldThrowForNonOrganizer`,
  `Execute_ShouldThrowForStranger`, `Execute_ShouldThrowForUnknownEvent` and
  `Execute_ShouldThrowForAlreadyDeletedEvent` all pass, each leaving the event
  intact on failure (REQ-005, REQ-006).
- [x] `DeleteEventTests.Execute_ShouldDeletePasscodeProtectedEvent` deletes a
  passcode-protected event with no passcode supplied (REQ-011).
- [x] No migration is added and no schema change occurs — no new file under
  `src/FindAtime.Api/Migrations/`, `FindatimeDbContextModelSnapshot.cs` untouched
  (REQ-016).
- [x] `Common/ExceptionHandler.cs`, the params/join/leave routes and all
  availability sources are unchanged by this task, and no new error code or
  exception type exists (REQ-017, REQ-018, REQ-019).
- [x] The full suite passes, so the existing events + availability contracts are
  unaffected (REQ-017).

**Verification:**
- `DOTNET_ROOT=/home/ar/.dotnet dotnet build`
- `podman compose up -d` (Testcontainers PostgreSQL must be able to start a container)
- `DOTNET_ROOT=/home/ar/.dotnet dotnet test tests/FindAtime.IntegrationTests/FindAtime.IntegrationTests.csproj --filter "DeleteEventTests"` → 7 passed
- `DOTNET_ROOT=/home/ar/.dotnet dotnet test` → full suite green
- `grep -n "MapDelete" -A18 src/FindAtime.Api/Features/Events/Api/EventsRestService.cs` → shows `Results.NoContent()` and both `BadRequest` messages
- `grep -rn "AddScoped<DeleteEvent>" src/FindAtime.Api/Composition/ServiceCollectionExtensions.cs tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` → one match each
- `git status --short src/FindAtime.Api/Migrations/` → empty (no migration)
- `git diff --stat src/FindAtime.Api/Common/ExceptionHandler.cs src/FindAtime.Api/Features/Availability/ src/FindAtime.Api/Features/Events/Domain/` → empty

Verified: 2026-11-01T19:19:58Z

## Task 2 — `events.hurl` wire entries (three 400s, delete, post-delete 404) + TypeScript client regen

**Depends on:** Task 1

**Goal:** Prove the new contract at the wire level and regenerate the TypeScript
client so the frontend can call delete: three request-shape 400 entries, the
organizer's 204 delete as the last mutating entry, a final `GET` asserting 404,
and a client method for `DELETE /events/{publicId}`.

**Work:**
- Edit `src/FindAtime.Api/Features/Events/Api/events.hurl` header entry list
  (currently lines 9–15) to exactly the design's renumbered list:
  1 `POST /events`, 2 `GET /events?guestId=`, 3 `GET /events/{publicId}`,
  4 `PUT /events/{publicId}/params` (X-Guest-Id; timezone + intervalMinutes
  required), 5 `POST /events/{publicId}/join`, 6 `POST /events/{publicId}/leave`,
  7 `DELETE /events/{publicId}` (missing X-Guest-Id → 400),
  8 `DELETE /events/{publicId}` (blank X-Guest-Id → 400),
  9 `DELETE /events/{publicId}` (malformed X-Guest-Id → 400),
  10 `DELETE /events/{publicId}` (X-Guest-Id; organizer only → 204),
  11 `GET /events/{publicId}` (assert 404 after delete).
- Append after the current leave entry (line 79, `HTTP 204`) the five entries, in
  this order, reusing the captured `{{publicId}}` and the static
  `{{organizerGuestId}}`:
  - `# Delete with a missing X-Guest-Id header (request-shape 400)` →
    `DELETE {{baseUrl}}/events/{{publicId}}` (no header) → `HTTP 400`.
  - `# Delete with a blank X-Guest-Id header (request-shape 400)` →
    `DELETE {{baseUrl}}/events/{{publicId}}` with `X-Guest-Id:` (empty value) →
    `HTTP 400`.
  - `# Delete with a malformed X-Guest-Id header (request-shape 400)` →
    `DELETE {{baseUrl}}/events/{{publicId}}` with `X-Guest-Id: not-a-uuid` →
    `HTTP 400`.
  - `# Delete the event (organizer only)` →
    `DELETE {{baseUrl}}/events/{{publicId}}` with
    `X-Guest-Id: {{organizerGuestId}}` → `HTTP 204`.
  - `# Assert the event is gone` → `GET {{baseUrl}}/events/{{publicId}}` →
    `HTTP 404`.
  The success `DELETE` must stay the last mutating entry and nothing after the
  final `GET` may depend on the event still existing.
- Run `DOTNET_ROOT=/home/ar/.dotnet make typescript-client` (target at
  `Makefile:6-8`: Release build of `src/FindAtime.Api/FindAtime.Api.csproj`, then
  `npm install && npm run prepare` in `typescript-client`) and keep the
  regenerated `typescript-client/dist/generated/**` files
  (`sdk.gen.d.ts`, `types.gen.d.ts`, `index.d.ts` and siblings) in the change.
- Confirm by inspection that the regenerated client exposes the delete method
  (operation id from `.WithName("DeleteEvent")` → `deleteEvent`, with
  `DeleteEventData` / `DeleteEventResponses` carrying `url: '/events/{publicId}'`
  and the 204 response), mirroring how `leaveEvent`/`LeaveEventData` appear today.
  Do not hand-edit generated files; if the method is missing, re-run the regen.

**Acceptance criteria:**
- [x] `events.hurl` contains three `DELETE /events/{{publicId}}` entries that
  assert `HTTP 400` — one with no `X-Guest-Id` header, one with a blank header,
  one with a non-UUID header — placed before the success delete
  (REQ-004, REQ-022; SC-006).
- [x] `events.hurl` contains the organizer `DELETE` with
  `X-Guest-Id: {{organizerGuestId}}` asserting `HTTP 204`, and it is the last
  mutating entry (REQ-003, REQ-020; SC-001).
- [x] The final entry of `events.hurl` is `GET {{baseUrl}}/events/{{publicId}}`
  asserting `HTTP 404`, the wire-level proof the event is gone (REQ-012, REQ-020).
- [x] The `events.hurl` header entry list enumerates entries 1–11 and matches the
  actual entry order and methods (REQ-020).
- [x] The regenerated TypeScript client (`typescript-client/dist/generated/**`)
  exposes a method for `DELETE /events/{publicId}` and the OpenAPI models are
  updated; no other endpoint disappears from the client (REQ-021, REQ-019).
- [x] No further id-dependent hurl entry follows the post-delete `GET`, so the
  file stays runnable end to end (REQ-020).

**Verification:**
- `DOTNET_ROOT=/home/ar/.dotnet dotnet build`
- `DOTNET_ROOT=/home/ar/.dotnet dotnet test`
- `DOTNET_ROOT=/home/ar/.dotnet make typescript-client`
- `grep -n "deleteEvent\|DeleteEventData\|DeleteEventResponses\|/events/{publicId}" typescript-client/dist/generated/sdk.gen.d.ts typescript-client/dist/generated/types.gen.d.ts` → the delete method and its `url: '/events/{publicId}'` are present
- `grep -n "^# \|^DELETE \|^GET \|^POST \|^PUT \|^HTTP " src/FindAtime.Api/Features/Events/Api/events.hurl` → 11 entries; entry 7–9 are DELETE → 400, entry 10 DELETE → 204, entry 11 GET → 404
- With the API running (`podman compose up -d`, `DOTNET_ROOT=/home/ar/.dotnet dotnet ef database update --project src/FindAtime.Api/FindAtime.Api.csproj`, `DOTNET_ROOT=/home/ar/.dotnet dotnet run --project src/FindAtime.Api/FindAtime.Api.csproj --launch-profile http`):
  `hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl` → all 11 entries pass
  (and `hurl --to-entry 10 src/FindAtime.Api/Features/Events/Api/events.hurl` to inspect the delete response). If no live API run is feasible in the environment, the grep-based ordering check above is the substitute and the post-delete 404 is additionally covered by `DeleteEventTests`.

Verified: 2026-11-01T19:19:58Z

---

**Fold target:** this change folds into `Docs/specs/events/` (delete endpoint,
cascade semantics, error codes, no schema change). `Docs/specs/availability/`
needs no delta: no availability code changes — the removal of availability rows is
a database-level consequence of the already-documented cascade FKs.

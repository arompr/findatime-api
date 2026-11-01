# 005-event-deletion: Research

## Tech stack

- **.NET 11 (net11.0)** — ASP.NET Core minimal APIs (no controllers/MVC), `Nullable` + `ImplicitUsings` enabled, root namespace `findatime_api`.
- **EF Core 11 + Npgsql** — PostgreSQL persistence and migrations under `src/FindAtime.Api/Migrations/`.
- **Dapper** — raw SQL reads loaded from embedded `.sql` resources via `Common/Sql.cs`.
- **PostgreSQL 18** — local container via Podman.
- **OpenAPI** — auto-generated via `AddOpenApi()`/`MapOpenApi()`; `QUERY` availability endpoints are not emitted.
- **Tests** — xunit; `FindAtime.UnitTests` for domain/converter unit tests; `FindAtime.IntegrationTests` for use-case + repository tests against Testcontainers.PostgreSQL.
- **Wire scratch files** — Hurl (`events.hurl`, `availability.hurl`).
- **TypeScript client** — regenerated via `make typescript-client`.

## Relevant modules

### Events feature (primary)
- `src/FindAtime.Api/Features/Events/Domain/Event.cs` — aggregate root; owns the `Participants` collection; `OrganizerParticipantId` references one of its participants.
- `src/FindAtime.Api/Features/Events/Domain/Participant.cs` — child entity of `Event`; carries `GuestId`, `EventId`, `Timezone`.
- `src/FindAtime.Api/Features/Events/Infra/DbContext.cs` — EF mappings; `Event`→`Participants` relationship is implicitly required and the snapshot models it `OnDelete(DeleteBehavior.Cascade)`.
- `src/FindAtime.Api/Features/Events/Infra/EventRepository.cs` — write-side repository; `Save` adds/updates the aggregate, `GetByPublicId` loads it with `Include(e => e.Participants)`.
- `src/FindAtime.Api/Features/Events/Application/LeaveEvent.cs` — closest command precedent: load by public id, organizer guard, mutate aggregate, save.
- `src/FindAtime.Api/Features/Events/Application/UpdateEventParams.cs` — organizer-only command precedent using `X-Guest-Id` and `ForbiddenException`.
- `src/FindAtime.Api/Features/Events/Application/Exceptions/ForbiddenException.cs` / `EventNotFoundException.cs` / `OrganizerCannotLeaveException.cs` — application exception patterns.
- `src/FindAtime.Api/Features/Events/Api/EventsRestService.cs` — maps endpoints; uses inline `Results.BadRequest` for request-shape issues and `X-Guest-Id` validation.
- `src/FindAtime.Api/Features/Events/Infra/Read/ReadEventService.cs` + `get_event_by_public_id.sql` / `search_events.sql` — Dapper read side; both return null/empty for a missing event today.

### Availability feature (cascade target)
- `src/FindAtime.Api/Features/Availability/Domain/Availability.cs` — pure entity referencing only `ParticipantId` (no `EventId`); stored as UTC instants.
- `src/FindAtime.Api/Features/Availability/Infra/AvailabilityRepository.cs` — EF write side; exposes `DeleteForParticipant` + `Save`.
- `src/FindAtime.Api/Features/Availability/Infra/DbContext.cs` (within the shared `DbContext`) — `Availability` has `HasOne<Participant>().WithMany().HasForeignKey(a => a.ParticipantId).OnDelete(DeleteBehavior.Cascade)`.
- `src/FindAtime.Api/Features/Availability/Infra/Read/get_event_availabilities.sql` + `get_participant_availability.sql` — read queries join `events`→`participants`→`availabilities`; deleting the event naturally makes these return empty.
- `src/FindAtime.Api/Features/Availability/Application/SetAvailability.cs` — loads the `Event` aggregate (with participants) and calls `AvailabilityRepository.DeleteForParticipant` before save; after event deletion this path will simply fail earlier with `EventNotFoundException`.

### Cross-cutting
- `src/FindAtime.Api/Common/ExceptionHandler.cs` — central dictionary mapping exception types to HTTP status + machine-readable `error` code.
- `src/FindAtime.Api/Common/Sql.cs` — loads embedded SQL resources by filename.
- `src/FindAtime.Api/Composition/ServiceCollectionExtensions.cs` — manual DI registration (`AddFindAtimeServices`).
- `src/FindAtime.Api/Program.cs` — composition root; maps `app.MapEvents()` and `app.MapAvailability()`.

### Tests
- `tests/FindAtime.IntegrationTests/Application/Events/LeaveEventTests.cs` — command test pattern (seed with `CreateEvent`/`JoinEvent`, assert via `EventRepository`).
- `tests/FindAtime.IntegrationTests/Application/Events/GetEventTests.cs` — read-side null-for-missing precedent.
- `tests/FindAtime.IntegrationTests/Application/Availability/SetAvailabilityTests.cs` — seeds participants + availability and inspects `DbContext.Availabilities` directly.
- `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` — manual DI setup; any new use case (`DeleteEvent`) must be registered here.

## Existing patterns

- **Aggregate ownership**: `Event` is the root aggregate and owns `Participant`. `Availability` is a separate aggregate that references `Participant` by id.
- **Expected failures are application exceptions** mapped in `Common/ExceptionHandler.cs`:
  - `EventNotFoundException` → 404 `event_not_found`
  - `ParticipantNotFoundException` → 404 `participant_not_found`
  - `ForbiddenException` → 403 `forbidden`
  - `OrganizerCannotLeaveException` → 409 `organizer_cannot_leave`
  - `InvalidTimezoneException` → 400 `invalid_timezone`
  - `InvalidIntervalException` → 400 `invalid_interval`
- **Inline `Results.BadRequest`** is used for request-shape issues: missing/blank `X-Guest-Id`, malformed UUID.
- **Organizer-only commands** (`UpdateEventParams`) read the `X-Guest-Id` header, parse it, load the event, find the organizer participant via `OrganizerParticipantId`, compare `GuestId`, and throw `ForbiddenException` if mismatched.
- **Read/write separation (CQRS-lite)**: writes use EF Core; reads use Dapper + embedded `.sql` files.
- **No HTTP-level integration tests**; integration tests exercise use cases directly against Testcontainers PostgreSQL.
- **Hurl files are workflows**: earlier entries capture ids used by later entries; deleting an event in `events.hurl` would need to be placed so later entries do not depend on the deleted event.
- **Migrations are EF Core code-first**; schema changes require `dotnet ef migrations add`.

## Feature spec summary

**Events** are the root aggregate. An organizer creates an event and becomes its first participant; other guests join by public id, optionally supplying a passcode. Event-level settings live in `EventParams` (`Timezone`, `IntervalMinutes`). The organizer can update params (`PUT /events/{publicId}/params`) and participants can leave (`POST /events/{publicId}/leave`). The current specs explicitly list "Editing or deleting an event after creation" as out of scope.

**Availability** belongs to a participant. Participants submit wall-clock ranges, which are snapped to the event interval, converted to UTC, and stored. Reads render stored UTC ranges back to a resolved viewer frame. The `availabilities` table has a FK to `participants` with `ON DELETE CASCADE`.

This change introduces organizer-only event deletion. When an organizer deletes an event, every participant and every participant's availability ranges must disappear with it, regardless of whether other participants are still in the event.

## Aggregate / persistence reality

- `events` table is the parent of `participants.event_id` via FK `FK_participants_events_event_id` with `onDelete: ReferentialAction.Cascade` (migration `20260908005715_PersistDomainEntities`).
- `availabilities` table is the child of `participants.id` via FK `FK_availabilities_participants_participant_id` with `onDelete: ReferentialAction.Cascade` (migration `20260920000223_AddAvailabilities`).
- EF Core snapshot confirms both relationships model `DeleteBehavior.Cascade`:
  - `Participant` → `Event` (`EventId`)
  - `Availability` → `Participant` (`ParticipantId`)
- Therefore, deleting an `Event` row from the database will automatically delete its `participants` rows, which will automatically delete the related `availabilities` rows.
- The `Event` aggregate in code already includes `Participants`; calling `_dbContext.Events.Remove(event)` and `SaveChangesAsync()` will generate deletes for participants and, by FK cascade, for availabilities. No explicit `AvailabilityRepository` delete is required if the delete is done through EF on the event aggregate.
- The Dapper read side depends only on these tables; after deletion, `get_event_by_public_id.sql` returns null (404), `search_events.sql` no longer joins to the deleted event, and availability reads return empty because the `events`/`participants` rows are gone.

## Delete mechanics options

1. **EF Core `Remove` on the event aggregate**
   - Load `Event` with `Include(e => e.Participants)` via `EventRepository.GetByPublicId`, call `_dbContext.Events.Remove(domainEvent)`, then `SaveChangesAsync()`.
   - Pros: stays in the write-side pattern; cascade deletes participants and (via DB FK) availabilities in one transaction.
   - Cons: loads all participants into memory; for very large events this is unnecessary overhead. Requires the cascade on `participants.event_id` to be trusted (it is, per migration + snapshot).

2. **Raw SQL `DELETE FROM events WHERE public_id = @publicId`**
   - Pros: no loading of participants; single round-trip; DB FK cascades handle children.
   - Cons: bypasses the domain model and EF change tracking; introduces a second write style; must still validate organizer before issuing delete.

3. **Explicit deletes in the use case**
   - Load event, then manually delete availabilities for every participant, then delete participants, then delete event.
   - Pros: explicit, easy to reason about, no reliance on cascade behavior.
   - Cons: verbose; multiple round-trips/queries; duplicates what the DB cascade already does.

The existing codebase already relies on cascade for availability (`DeleteForParticipant` is used only on availability replace, not on participant removal). Removing the event aggregate through EF is the most consistent option; raw SQL is the most performant. The design phase must choose between consistency+simplicity and raw efficiency.

## Use-case + API precedent

### Existing command shape (`LeaveEvent`)
- Load event via `EventRepository.GetByPublicId`.
- Resolve guest id.
- Apply domain rule (organizer cannot leave).
- Mutate aggregate (`RemoveParticipant`).
- `await _eventRepository.Save(domainEvent)`.
- Returns `Task` (no result); endpoint returns `204 NoContent`.
- Missing event → `EventNotFoundException` → 404.

### Existing organizer-only command shape (`UpdateEventParams`)
- Load event via `EventRepository.GetByPublicId`.
- Find organizer participant via `domainEvent.FindParticipant(domainEvent.OrganizerParticipantId)`.
- Compare `organizer.GuestId` to request `GuestId`; mismatch → `ForbiddenException` → 403.
- Apply mutation.
- Save and return response.

### Plausible `DELETE /events/{publicId}` endpoint
- Read `X-Guest-Id` header; blank/malformed → inline `Results.BadRequest`.
- Call a new `DeleteEvent.Execute(publicId, guestId)` use case.
- Use case loads event, throws `EventNotFoundException` if missing, checks organizer, throws `ForbiddenException` if not organizer, then deletes event.
- Return `204 NoContent` (matching `LeaveEvent`) or `200 Ok` with an empty/deleted body.

### ExceptionHandler mapping today
- `ForbiddenException` → 403 `forbidden`
- `EventNotFoundException` → 404 `event_not_found`
- No mapping needed for a successful delete.

## Read-side behavior after deletion

- `GET /events/{publicId}` already throws `EventNotFoundException` when `ReadEventService.GetEventByPublicId` returns `null`; after deletion this remains 404.
- `GET /events?guestId=` already excludes events the guest does not participate in; after deletion the event is gone from search results.
- `GET /events/{publicId}/participant` and the two `QUERY` availability reads join through `events`/`participants`; after deletion they will return `null`/empty. `GetMyParticipant` throws `EventNotFoundException` first if the event is missing; `GetEventAvailabilities`/`GetParticipantAvailability` read availability first and will return empty participant lists if the event does not exist. This may need alignment (return 404 vs 200 empty) — open question.

## Test landscape

- **Use-case command tests**: `tests/FindAtime.IntegrationTests/Application/Events/LeaveEventTests.cs` is the closest pattern. A new `DeleteEventTests.cs` would:
  - Seed with `CreateEvent.Execute` and optionally `JoinEvent.Execute`.
  - Call `DeleteEvent.Execute`.
  - Assert via `EventRepository.GetByPublicId` returning `null` and `DbContext.Availabilities` returning no rows for the deleted participants.
- **Integration fixture**: add `services.AddScoped<DeleteEvent>();` in `IntegrationTest.cs`.
- **Unit tests**: probably none required; deletion is pure orchestration.
- **HTTP tests**: not present in this repo; the only wire-level verification is via Hurl.

## Hurl files

### `events.hurl`
Current entries:
1. `POST /events` — creates event, captures `publicId`.
2. `GET /events?guestId=` — search.
3. `GET /events/{publicId}` — get event.
4. `PUT /events/{publicId}/params` — update params.
5. `POST /events/{publicId}/join` — join, captures `participantId`.
6. `POST /events/{publicId}/leave` — leave.

A delete entry should be added **after the join entry** (so there is a participant to delete with) and **after any entries that need the event to exist**. If placed at the end (entry 7), it will not break earlier entries. To prove availability is also removed, the delete entry could be followed by a `GET /events/{publicId}` asserting 404, or a separate availability workflow could be added to `availability.hurl`.

### `availability.hurl`
Current entries build an event, join a participant, set availability, and read it. A delete entry here does not fit naturally because availability endpoints have no delete; event deletion is an events concern. The best place to demonstrate "availability disappears with event" is either:
- a new final entry in `events.hurl` that sets availability before deleting, or
- a new `availability.hurl` entry that attempts to read availability after an event delete (requires cross-file captures, which Hurl does not support).

Recommendation: keep the delete demonstration in `events.hurl` and add an assert that `GET /events/{publicId}` returns 404 after deletion.

## TypeScript client regeneration

- `make typescript-client` builds the API in Release and runs `npm run prepare` in `typescript-client/`.
- Adding `DELETE /events/{publicId}` will generate a new SDK method and update OpenAPI types.
- The two `QUERY` availability endpoints remain absent from the generated client (pre-existing limitation).
- The FE handoff doc should note the new delete method.

## Precedent trace: changes 002/003/004

| Layer | What the pattern looks like | Analog for delete |
|-------|----------------------------|-------------------|
| **Migration** | 003: no migration; 004: added `interval_minutes` column. | Likely **no migration**: the cascade FKs already exist; only code changes. |
| **EF write mapping** | `DbContext` already models `Event`→`Participants` cascade and `Availability`→`Participant` cascade. | No mapping change needed if using EF `Remove(event)`. |
| **Domain** | `Event.AddParticipant`/`RemoveParticipant` mutate the aggregate. | Could add `Event.Delete()` (no-op marker) or delete directly in use case. |
| **Application** | `LeaveEvent`/`UpdateEventParams` load by public id, enforce rules, mutate/save. | New `DeleteEvent` use case: load, organizer check, delete event, save. |
| **API request/response** | `LeaveEventRequestParams` (body with `guestId`) and `204`; `UpdateEventParams` uses `X-Guest-Id`. | `DELETE` uses `X-Guest-Id` header, no body, returns `204` or `200`. |
| **Endpoint** | `EventsRestService.MapEvents` adds route with inline validation and `.Produces(...)`. | Add `app.MapDelete("/events/{publicId}", ...)`. |
| **DI** | Register new use case in `ServiceCollectionExtensions.cs` and `IntegrationTest.cs`. | Register `DeleteEvent` scoped in both places. |
| **Tests** | `LeaveEventTests` seeds via use cases and asserts repository state. | New `DeleteEventTests` with organizer, non-organizer, missing-event, and availability-cascade cases. |
| **Hurl** | `events.hurl` entry updated/added for each new behavior. | Add a delete entry and a post-delete 404 assert. |
| **TS client** | `make typescript-client` after endpoint changes. | Regenerate after adding the DELETE route. |

## Scope OUT reminders

- **Passcode editing** (the deferred second todo) is explicitly out of scope. This change must not modify the `PUT /events/{publicId}/params` endpoint beyond what deletion itself requires (which is nothing).
- Do not add a generic event-edit endpoint.
- Do not add participant notifications, audit history, or soft-delete features unless the proposal phase explicitly decides otherwise.

## Open questions / risks

1. **Hard delete vs soft delete**
   - Hard delete matches the brief language ("delete his events") and is simplest.
   - Soft delete would require a new `deleted_at` column, migration, and filtering on every read query. The brief does not ask for recovery or audit.

2. **Idempotency of deleting a missing/already-deleted event**
   - Option A: return `404 event_not_found` (consistent with `GET`/`Join`/`Leave` missing event behavior).
   - Option B: return `204 NoContent` (idempotent delete, common REST pattern).
   - The existing codebase uses `EventNotFoundException` for all missing-event commands (`Leave`, `Join`, `UpdateEventParams`), so 404 is the local convention.

3. **Response status**
   - `204 NoContent` matches `LeaveEvent` precedent and is natural for DELETE.
   - `200 OK` with a body is not used elsewhere for destructive commands.

4. **Auth failures**
   - Missing/blank `X-Guest-Id` → inline `400 BadRequest` (consistent with `UpdateEventParams`/`SetAvailability`).
   - Malformed `X-Guest-Id` → inline `400 BadRequest`.
   - Non-organizer → `ForbiddenException` → 403 `forbidden`.

5. **Availability read behavior for deleted events**
   - `GetEventAvailabilities` currently returns a 200 with an empty participant list if the event does not exist (it reads `events` first to get timezone, then participants). After deletion it will return empty.
   - Should the availability reads return `404 event_not_found` when the event is gone? `GetMyParticipant` already does. This is an inconsistency that may or may not need fixing in this change.

6. **DB migration needs**
   - Probably **none**: the required cascade FKs are already in place (`events`→`participants` and `participants`→`availabilities`).
   - Must verify during design/apply that the actual deployed schema matches the snapshot (cascade on both FKs).

7. **EF vs raw-SQL delete**
   - EF `Remove(event)` is consistent with existing write-side patterns but loads all participants.
   - Raw SQL is more efficient but introduces a second write style.
   - A hybrid (load event for organizer check, then raw SQL delete) is also possible.

8. **Participant notification**
   - The brief says nothing about notifying participants. Almost certainly out of scope; record it so proposal does not accidentally add it.

9. **Passcode behavior**
   - Deletion of a passcode-protected event is the same as an unprotected event (organizer only, via `X-Guest-Id`); no passcode is required to delete.

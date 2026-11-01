# 005-event-deletion: Proposal

## Goal / Why

Today an organizer can create an event, invite people to it, adjust its
params — and then never get rid of it. There is no delete, no archive, no
"cancel". The only exit for the event itself is the participant-level
`POST /events/{publicId}/leave`, and the organizer is explicitly barred from
using it (`OrganizerCannotLeaveException` → 409). The organizer is the one
person who *wants* the event gone, and they are the one person who cannot
remove themselves from it.

The result is that every event ever created stays alive forever. It shows up
in `GET /events?guestId=` for every guest who ever joined it, participants
keep seeing it in their lists, and the organizer keeps seeing it in theirs.
A "pizza Friday" poll from six months ago is indistinguishable from the
meeting being scheduled right now, and there is no way to tell the two apart.
Stale events are not a rare edge case — they are the normal residue of a
product whose whole purpose is creating throwaway scheduling events.

This change gives the organizer one way out: **delete the event**. The event
row is removed, and with it every participant and every participant's
availability ranges, regardless of how many people are still in the event.
The brief is explicit that other participants still being present must not
block the deletion — a host whose event time was called off cannot be held
hostage by a guest who never responded.

Deletion is **hard**. There is no soft-delete flag, no `deleted_at` column, no
recovery, no audit trail. The event is gone, and so is everything that
belongs to it.

## Scope IN

**Application (events)** — a new `DeleteEvent` use case
(`Features/Events/Application/DeleteEvent.cs`) following the `UpdateEventParams`
shape: load the event by public id, throw `EventNotFoundException` if it is
missing, resolve the organizer via `OrganizerParticipantId` and throw
`ForbiddenException` on a guest-id mismatch, then delete.

**Domain (events)** — no domain change beyond what deletion strictly needs.
`Event` keeps its aggregate shape; the deletion itself is a persistence
operation, not a domain rule. If design needs a small marker (e.g. an
`Event.Delete()`), that is design's call — the proposal does not require one.

**Persistence (events)** — the delete goes through EF Core on the loaded
aggregate (`DbContext.Events.Remove(event)` → `SaveChangesAsync()`), which
relies on the two cascade FKs already in the schema (`events→participants`,
`participants→availabilities`). **No migration** — the schema already
supports this.

**API (events)** — a new `DELETE /events/{publicId}` endpoint in
`EventsRestService`, using the `X-Guest-Id` header convention that
`PUT /events/{publicId}/params` already established for organizer-only
commands. Request-shape validation (missing/blank/malformed `X-Guest-Id`)
stays inline as `Results.BadRequest`, matching `UpdateEventParams`.

**DI** — `DeleteEvent` registered as `Scoped` in
`Composition/ServiceCollectionExtensions.cs` (production) and in
`tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` (test fixture).

**Tests** — a new `tests/FindAtime.IntegrationTests/Application/Events/DeleteEventTests.cs`
covering: organizer deletes their own event (event row gone); non-organizer
is rejected with `ForbiddenException`; missing event raises
`EventNotFoundException`; the event is deleted **even when other participants
are still joined**; and the availability cascade — `DbContext.Availabilities`
has no rows left for the deleted event's participants. The last assertion is
the one that proves the cascade FKs are actually in force, not just that the
event row disappeared.

**Wire scratch file** — `events.hurl` gains a delete entry.

**TypeScript client** — regenerated (`make typescript-client`) so the new
`DELETE /events/{publicId}` method exists for the frontend.

**Feature specs** — folded into `Docs/specs/events/` at archive time. Note
that `Docs/specs/events/requirements.md` currently lists *"Editing or deleting
an event after creation"* under **Out of Scope**; that line is split, since
deletion is now in scope while editing and passcode editing remain out.

## Scope OUT

- **Passcode editing** — the deferred second todo from the brief. Explicitly
  out of scope. `PUT /events/{publicId}/params` is not touched by this change.
- **Soft delete, `deleted_at` column, restore, undo, recovery.** Hard delete
  only. A deleted event cannot be brought back.
- **Audit history / event tombstones.** No record of a deleted event is kept.
- **Participant notifications.** Nothing is emitted when an event is deleted —
  no email, no webhook, no in-app notice, no `notifyParticipants` flag on the
  endpoint. Participants simply find the event gone.
- **Any change to read behavior.** `GET /events/{publicId}`,
  `GET /events?guestId=`, `GET /events/{publicId}/participant`, and the two
  `QUERY` availability reads are not modified. Their post-deletion behavior
  follows from the rows being gone.
  - **Known issue, explicitly not fixed here:** the pre-existing
    inconsistency where `GetEventAvailabilities` / `GetParticipantAvailability`
    return `200` with an empty body for a missing event, while `GetEvent` and
    `GetMyParticipant` return `404`. This change makes that gap more visible
    (deletion is a routine way to reach it), but **this change does not modify
    read behavior**. It is recorded here so it is not lost, and should be
    addressed in its own change.
- **No generic event-edit endpoint.** No `PATCH /events/{publicId}`, no
  rename, no "cancel event" flag that is not a delete.
- **No changes to params, join, or leave behavior.** The existing endpoints
  keep their current contracts, statuses, and error codes.
- **No change to the `availabilities` schema, the `AvailabilityRepository`,
  or the availability read queries.** The cascade does the work.
- **No new application exception or error code.** `EventNotFoundException`
  (404 `event_not_found`) and `ForbiddenException` (403 `forbidden`) already
  exist and are already mapped in `Common/ExceptionHandler`.
- **No passcode requirement for deletion.** A passcode-protected event is
  deleted exactly like an unprotected one: organizer identity via
  `X-Guest-Id` is the only gate.
- **No HTTP-level test framework.** This repo has no HTTP integration tests;
  the wire-level check is the Hurl file.

## Decisions handed to spec/design (recommended answer given)

These are the points the brief and the research deliberately left open. Each
is stated with a recommendation so `sdd-spec` can adopt it directly, but each
is marked as a decision to confirm.

**Q1 — Response status for a successful delete.**
*Recommendation:* **204 NoContent**, with no body. This matches the existing
destructive-command precedent (`POST /events/{publicId}/leave` → 204) and is
the natural response for `DELETE`. No response DTO is needed — the frontend
already knows the event's public id, so echoing it back adds nothing.
*Confirm:* `204`, no body, no new response record.

**Q2 — Deleting a missing or already-deleted event.**
*Recommendation:* **404 `event_not_found`** via the existing
`EventNotFoundException`, not a REST-idempotent `204`. Every command in this
codebase treats a missing event as not found — `JoinEvent`, `LeaveEvent`, and
`UpdateEventParams` all throw `EventNotFoundException`. A `DELETE` that
answers `204` for an event that never existed would make the endpoint
indistinguishable from one that deleted something, and it would leak whether
a public id ever existed to a non-organizer caller. Note the tension with
HTTP idempotency and record it, but the local convention wins here: the
endpoint is organizer-scoped, and 404 is what the FE will already be handling
from `GET /events/{publicId}`.
*Confirm:* `404 event_not_found` for both never-existed and already-deleted.

**Q3 — Auth failure handling.**
*Recommendation:* split the failure modes exactly as `UpdateEventParams`
already does:
- Missing / blank / malformed `X-Guest-Id` → inline `Results.BadRequest`
  (request-shape failure, before any lookup) → `400`.
- Non-organizer guest id → `ForbiddenException` → `403 forbidden`.

Rationale: a malformed header is a malformed request and is rejected without
touching the database; an authorization failure is a domain rule and goes
through the central exception mapping. This keeps the endpoint's error codes
identical to `PUT /events/{publicId}/params`, so the FE can reuse one handler.
*Confirm:* the split above; no new error codes.

**Q4 — Delete mechanics: EF `Remove` vs. raw SQL vs. hybrid.**
*Recommendation:* **EF Core `Remove` on the loaded aggregate** —
`DbContext.Events.Remove(domainEvent)` followed by `SaveChangesAsync()`.
The event is already loaded (with `Include(e => e.Participants)`) to perform
the organizer check, so the delete is a change-tracker operation on an
already-tracked entity. This keeps one write style in the codebase (EF for
writes, Dapper for reads) and lets the existing cascade FKs do the
participant and availability cleanup.

The trade-off, stated plainly: EF will issue DELETE statements for the
loaded participants as well as the event, which is redundant with the
database's own `ON DELETE CASCADE`. This is an accepted cost — participant
counts on a scheduling event are small (tens), and correctness/consistency
with the write-side pattern is worth more here than shaving a round-trip.
Raw SQL was rejected because it introduces a second write style and bypasses
change tracking; the hybrid was rejected because it re-introduces the same
second write style while still paying for the load.
*Confirm:* EF `Remove` on the loaded aggregate; the extra participant DELETEs
are accepted.

**Q5 — Where the delete entry goes in `events.hurl`.**
*Recommendation:* **append at the end of the file (entry 7)**, followed by a
`GET /events/{publicId}` asserting `404`. The file is an ordered workflow
where later entries depend on earlier captures, so the delete must come after
every entry that needs the event to exist. Placing it last means it breaks
nothing. The follow-up 404 `GET` is the wire-level proof that the event is
gone; it is not a substitute for the cascade assertion in
`DeleteEventTests`, because Hurl cannot read the `availabilities` table.
*Confirm:* delete as the final entry, then a 404 `GET /events/{publicId}`.

**Q6 — Should `DeleteEventTests` prove the availability cascade?**
*Recommendation:* **yes.** The brief's core claim — "even if people are still
in the event, an organizer should be able to delete his events" — is only
fully proven if the participants *and their availability* disappear with it.
Asserting that `DbContext.Availabilities` has no rows for the deleted
participants is the direct test of the `participants→availabilities` cascade
FK. Without it, a regression that deleted only the event row (or that broke
the cascade) would pass silently, because the availability rows would be
orphaned and invisible to every other assertion in the file.
*Confirm:* yes — the cascade assertion is a required test case.

**Q7 — Does the endpoint require a request body?**
*Recommendation:* **no.** The organizer is identified by the `X-Guest-Id`
header only, and `DELETE` with a body is awkward for HTTP clients. This
differs from `POST .../leave` (which carries `guestId` in a body) but matches
`PUT .../params`, which is the closer analogue: an organizer-only command
keyed on the same header.
*Confirm:* header-only, no body.

## Rationale

- **The organizer is the only person who can want an event gone, and today is
  the only person who cannot express that.** Every other participant can
  remove themselves with `leave`; the organizer is blocked by a deliberate
  domain rule (`OrganizerCannotLeaveException`). That rule makes sense for
  *leaving* — it stops the host from accidentally orphaning an event — but it
  leaves no legitimate way to end an event that was cancelled. Delete is that
  way.
- **Blocking deletion while participants remain would defeat the purpose.**
  The brief calls this out directly, and the product logic agrees: the
  organizer's obligation is to the event, not to the guest list. A host whose
  Tuesday meeting was called off must be able to call it off even if three
  people had said they were free. Gating on an empty participant list would
  mean the worst case — an event nobody ever joined — is the *only* one that
  can be deleted.
- **Hard delete, because the schema already does the right thing.** Both
  cascade FKs (`events→participants`, `participants→availabilities`) already
  exist. A soft delete would require a new column, a migration, and a filter
  on every read query — `search_events.sql`, `get_event_by_public_id.sql`,
  and both availability reads — for a feature nobody asked for. There is no
  recovery requirement, no audit requirement, and no compliance requirement
  in this product. The honest version of "delete" is "the rows are gone".
- **Organizer-only, keyed on `X-Guest-Id`, exactly like the params command.**
  The codebase already has one organizer-only command and one way to express
  it. A delete endpoint that invented a second auth mechanism would be a
  second thing to learn, test, and get wrong.
- **`204`, `404`, `400`, `403` — the existing vocabulary.** No new status
  codes, no new error codes, no new exception types. The endpoint reads as
  "the params command, but destructive".
- **Proving the cascade in tests, not just in prose.** The available data
  outliving its event is the failure mode that would actually hurt (orphaned
  rows, confusing reads, possible FK violations on the next participant
  write). It is also the failure mode that a naive "the event row is gone"
  assertion would miss entirely.

## Risks

- **Irreversibility.** A deleted event cannot be recovered by anyone,
  including the organizer. Mitigation: the deletion is explicitly
  organizer-scoped, so the blast radius is one person's own event; the
  frontend should confirm before calling the endpoint. Accepted, per the
  brief's hard-delete decision.
- **The cascade depends on the deployed schema, not just the EF snapshot.**
  Both cascade FKs were confirmed in the migrations and the EF snapshot, but
  the `DeleteEventTests` cascade assertion is what actually proves it at
  runtime — which is why Q6 recommends making it a required test case rather
  than a nice-to-have.
- **Participants lose their submitted availability with no notice.** That is
  the intended consequence of a hard delete, but it is worth stating: a
  participant who submitted ranges has them erased with no notification and
  no way to know. Mitigation: out of scope for this change, but the frontend
  should copy the event's state before offering a delete, and a future change
  could add notifications.
- **The 404-vs-200-empty read inconsistency becomes more visible.** After a
  delete, `GET /events/{publicId}` returns `404` while
  `QUERY /events/{publicId}/availabilities` returns `200` with an empty
  participant list. This is pre-existing behavior that this change does not
  touch, but it is now reachable by a routine path. Mitigation: recorded here
  and in Scope OUT as a separate change; the FE must not assume "empty list"
  means "no one has submitted availability" — it can also mean "no such
  event".
- **Redundant participant DELETEs from EF.** EF will delete the loaded
  participants explicitly in addition to the database cascade. Accepted cost
  of the consistency-first choice (Q4); participant counts are small.
- **The frontend needs the new method before it can ship a delete button.**
  The TypeScript client is regenerated as part of this change, but the
  backend and the client must ship in lockstep, as with the `intervalMinutes`
  body change in change 004.
- **Public ids are guessable enough to be probed.** A non-organizer calling
  `DELETE` gets `403`, and a nonexistent public id gets `404` — a small
  information leak about which ids exist, consistent with every other
  endpoint in this API.

# 005-event-deletion: Spec

The **what**. Testable requirements and scenarios only — no implementation,
no architecture, no code.

## Requirements

### REQ-001: Delete endpoint exists
WHEN a client sends `DELETE /events/{publicId}` THEN THE SYSTEM SHALL expose an
endpoint that deletes the event identified by the shareable `{publicId}`.

### REQ-002: Organizer identified by header only, no request body
WHEN `DELETE /events/{publicId}` is called THEN THE SYSTEM SHALL identify the
caller solely from the `X-Guest-Id` header and SHALL accept no request body.

### REQ-003: Successful delete returns 204 with no body
WHEN the organizer deletes their own event THEN THE SYSTEM SHALL return HTTP
`204` with no response body and no response record.

### REQ-004: Missing, blank, or malformed X-Guest-Id rejected before any lookup
WHEN the `X-Guest-Id` header is missing, blank, or not a valid UUID THEN THE
SYSTEM SHALL reject the request with HTTP `400` (request-shape) without
performing any event lookup.

### REQ-005: Non-organizer is forbidden
WHEN a well-formed `X-Guest-Id` that does not match the event's organizer is
supplied — including any other participant or a stranger — THEN THE SYSTEM
SHALL reject with HTTP `403` and error code `forbidden`.

### REQ-006: Missing or already-deleted event returns 404, deletion is not idempotent
WHEN the public id does not identify an existing event — including a second
delete of an already-deleted event — THEN THE SYSTEM SHALL reject with HTTP
`404` and error code `event_not_found`. Deletion SHALL NOT be idempotent: a
second `DELETE` of the same public id returns `404`, never `204`.

### REQ-007: Deletion removes the event itself
WHEN the organizer deletes an event THEN THE SYSTEM SHALL remove the event such
that it no longer exists.

### REQ-008: Deletion removes every participant, regardless of count
WHEN the organizer deletes an event THEN THE SYSTEM SHALL remove every
participant of that event and SHALL succeed regardless of how many participants
are still joined.

### REQ-009: Deletion removes every participant's availability ranges
WHEN the organizer deletes an event THEN THE SYSTEM SHALL remove every
availability range belonging to every participant of that event.

### REQ-010: Deletion is hard and leaves no record
WHEN an event is deleted THEN THE SYSTEM SHALL leave no trace: no soft-delete
flag, no recovery path, no undo, and no audit record. Deletion SHALL be
irreversible.

### REQ-011: Passcode-protected events delete identically, no passcode required
WHEN a passcode-protected event is deleted THEN THE SYSTEM SHALL require no
passcode and SHALL behave exactly as for an unprotected event: organizer
identity via `X-Guest-Id` is the only gate.

### REQ-012: Post-deletion event read returns 404
WHEN `GET /events/{publicId}` is called for a deleted event THEN THE SYSTEM
SHALL return HTTP `404` `event_not_found` (existing behavior, unchanged).

### REQ-013: Post-deletion participant resolve returns 404
WHEN `GET /events/{publicId}/participant` is called for a deleted event THEN THE
SYSTEM SHALL return HTTP `404` `event_not_found`.

### REQ-014: Post-deletion availability reads return 404
WHEN either availability read — `QUERY /events/{publicId}/availabilities` or
`QUERY /events/{publicId}/participants/{participantId}/availability` — is called
for a deleted event THEN THE SYSTEM SHALL return HTTP `404` `event_not_found`.

### REQ-015: Post-deletion search no longer lists the event
WHEN `GET /events?guestId=` is called for any former participant THEN THE SYSTEM
SHALL NOT include the deleted event in the returned list.

### REQ-016: No schema change
WHEN this change is delivered THEN THE SYSTEM SHALL NOT alter the database
schema; no migration SHALL be required.

### REQ-017: Existing params, join, and leave contracts are unchanged
WHEN this change is delivered THEN THE SYSTEM SHALL leave the params, join, and
leave endpoint contracts — statuses, bodies, and error codes — unchanged.

### REQ-018: No new error codes or exception types
WHEN `DELETE /events/{publicId}` fails THEN THE SYSTEM SHALL reuse the existing
error codes (`event_not_found`, `forbidden`) and SHALL NOT introduce new error
codes.

### REQ-019: Availability behavior is otherwise unchanged
WHEN this change is delivered THEN THE SYSTEM SHALL NOT modify availability
read or write behavior other than the removal of availability that follows from
deleting its owning event.

### REQ-020: events.hurl gains a delete entry and a post-delete 404 assert
WHEN this change is delivered THEN `events.hurl` SHALL contain a delete entry as
its final entry, followed by a `GET /events/{publicId}` entry asserting HTTP
`404`.

### REQ-021: TypeScript client exposes the delete method
WHEN this change is delivered THEN the regenerated TypeScript client SHALL
expose a method for `DELETE /events/{publicId}`.

### REQ-022: Cascade and use-case failure modes proven in integration tests; request-shape 400s verified in events.hurl
WHEN this change is delivered THEN the integration test suite (`DeleteEventTests`)
SHALL prove: the organizer deletes their own event with participants still
present and the participants and their availability are all removed (the
cascade); a non-organizer is rejected with `403`; a missing event returns `404`;
and a second delete returns `404` — and the request-shape failures (missing,
blank, malformed `X-Guest-Id`) SHALL be verified at the wire level via
`events.hurl` entries returning `400`.

## Scenarios

### SC-001: Organizer deletes a populated event
GIVEN an event whose organizer and one or more other participants are joined,
some with submitted availability WHEN the organizer sends
`DELETE /events/{publicId}` with their own `X-Guest-Id` THEN the system SHALL
return `204` with no body, and the event, its participants, and their
availability SHALL all be gone.

### SC-002: Non-organizer participant is forbidden
GIVEN an event where a participant is not the organizer WHEN that participant
sends `DELETE /events/{publicId}` with their own `X-Guest-Id` THEN the system
SHALL return `403 forbidden` and SHALL leave the event and all its participants
intact.

### SC-003: Stranger is forbidden
GIVEN an event WHEN a guest id that is not a participant of the event sends
`DELETE /events/{publicId}` THEN the system SHALL return `403 forbidden` and
SHALL leave the event intact.

### SC-004: Missing event returns 404
GIVEN a public id that identifies no existing event WHEN
`DELETE /events/{publicId}` is called with a well-formed `X-Guest-Id` THEN the
system SHALL return `404 event_not_found`.

### SC-005: Double-delete returns 404
GIVEN an event the organizer has already deleted WHEN the organizer sends
`DELETE /events/{publicId}` again THEN the system SHALL return
`404 event_not_found`.

### SC-006: Missing, blank, or malformed header returns 400
GIVEN an existing event WHEN `DELETE /events/{publicId}` is called without an
`X-Guest-Id` header, with a blank header, or with a non-UUID header THEN the
system SHALL return `400` (request-shape) without any event lookup.

### SC-007: Passcode-protected event deletes the same way
GIVEN a passcode-protected event WHEN the organizer sends
`DELETE /events/{publicId}` with their `X-Guest-Id` and no passcode THEN the
system SHALL return `204` and delete the event exactly as an unprotected one.

### SC-008: Post-deletion reads all return 404
GIVEN an event the organizer has deleted WHEN `GET /events/{publicId}`,
`GET /events/{publicId}/participant`,
`QUERY /events/{publicId}/availabilities`, or
`QUERY /events/{publicId}/participants/{participantId}/availability` is called
THEN the system SHALL return `404 event_not_found`.

### SC-009: Post-deletion search omits the event
GIVEN an event the organizer has deleted WHEN any former participant calls
`GET /events?guestId=` THEN the event SHALL NOT appear in the returned list.

### SC-010: Availability ranges are removed with their participants
GIVEN an event whose participants have submitted availability ranges WHEN the
organizer deletes the event THEN the system SHALL leave no availability ranges
for any of the event's former participants.

## Acceptance Criteria

- [ ] `DELETE /events/{publicId}` exists and deletes the event identified by the shareable public id.
- [ ] The caller is identified solely by the `X-Guest-Id` header; no request body is accepted.
- [ ] A successful delete returns `204` with no body and no response record.
- [ ] A missing, blank, or malformed `X-Guest-Id` header returns `400` before any lookup.
- [ ] A well-formed non-organizer `X-Guest-Id` (other participant or stranger) returns `403 forbidden`.
- [ ] A missing event returns `404 event_not_found`.
- [ ] A second delete of the same public id returns `404 event_not_found` (not idempotent).
- [ ] Deletion removes the event, every participant (regardless of how many remain joined), and every participant's availability ranges.
- [ ] Deletion is hard: nothing remains, nothing is recoverable, and no record is kept.
- [ ] A passcode-protected event deletes identically with no passcode required.
- [ ] After deletion, `GET /events/{publicId}` returns `404 event_not_found`.
- [ ] After deletion, `GET /events/{publicId}/participant` returns `404 event_not_found`.
- [ ] After deletion, both availability `QUERY` reads return `404 event_not_found`.
- [ ] After deletion, `GET /events?guestId=` no longer lists the event for any former participant.
- [ ] No schema change / no migration is required.
- [ ] The params, join, and leave contracts are unchanged.
- [ ] No new error codes or exception types are introduced.
- [ ] Availability behavior is otherwise unchanged (the only change is cascade removal on delete).
- [ ] `events.hurl` ends with a delete entry followed by a `GET /events/{publicId}` asserting `404`.
- [ ] The regenerated TypeScript client exposes a `DELETE /events/{publicId}` method.
- [ ] Integration tests prove the availability cascade (participants and their availability are gone), organizer success with participants present, non-organizer `403`, missing `404`, and double-delete `404`; the request-shape `400`s are asserted in `events.hurl`.

# Initial Availability: Tasks

Ordered. Task 1 is the shared foundation and must land first; Tasks 2–4 each add
one endpoint on top of it.

## Task 1 — `GET /events/{publicId}/participant`

**Depends on:** none

**Goal:** Resolve the calling guest into the participant record for an event.

**Work:**
- DB migration `AddAvailabilities` (table + timezone columns + indexes).
- Domain: `Availability`, `AvailabilityId`, `AvailabilityFactory`;
  add `Timezone` to `Event` and `Participant`.
- Exceptions: `ParticipantNotFoundException`, `InvalidAvailabilityRangeException`,
  `ForbiddenException`; register in `Common/ExceptionHandler`.
- Infra: `DbContext` DbSet + mappings; `AvailabilityRepository` and
  `ReadAvailabilityService` stubs; `get_participant_by_event_and_guest.sql`.
- DI registrations in `Composition/ServiceCollectionExtensions`.
- Use case `GetMyParticipant` (+ `GetMyParticipantResult`), API
  `GetMyParticipantResponse`, endpoint in `AvailabilityRestService`.
- FE: `resolveParticipant`, `useMyParticipant`, wire into `EventPage`.

**Acceptance criteria:**
- [ ] `GET /events/{publicId}/participant` with valid `X-Guest-Id` returns `{ participantId, name }`
- [ ] 400 when `X-Guest-Id` missing or malformed
- [ ] 404 when event unknown or guest hasn't joined

**Verification:**
- Unit: `GetMyParticipant.Execute` found / `EventNotFoundException` / `ParticipantNotFoundException`.
- Integration: create → join → resolve; 404 cases; 400 cases.

## Task 2 — `GET /events/{publicId}/availabilities`

**Depends on:** Task 1

**Goal:** Return every participant's availability ranges for the heatmap.

**Work:**
- Use case `GetEventAvailabilities` (+ DTOs), API response records.
- `get_event_availabilities.sql` (two statements); `ReadAvailabilityService.GetAllForEvent`.
- Endpoint in `AvailabilityRestService`.
- FE: `getEventAvailabilities`, `useEventAvailabilities`, wire into `AvailabilityWeek`.

**Acceptance criteria:**
- [x] 200 returns flat participant list with grouped ranges (empty when no one joined)
- [x] 400 invalid publicId; 404 event not found

**Verification:**
- Unit: empty list / grouping / ordering / `eventTimezone` always present.
- Integration: seed participants + ranges, assert grouping + ordering.

## Task 3 — `GET /events/{publicId}/participants/{participantId}/availability`

**Depends on:** Task 1

**Goal:** Read one specific participant's availability.

**Work:**
- Use case `GetParticipantAvailability`, endpoint in `AvailabilityRestService`.
- `get_participant_availability.sql`; cross-check participant belongs to event.
- FE helper `getParticipantAvailability` + optional hook (no default wiring).

**Acceptance criteria:**
- [x] 200 returns participant ranges (empty when none)
- [x] 404 for unknown publicId / participantId / cross-event participant

**Verification:**
- Unit: empty ranges; cross-event throws.
- Integration: 404 cases + 200 grouped ranges.

## Task 4 — `PUT /events/{publicId}/participants/{participantId}/availability`

**Depends on:** Tasks 1–3

**Goal:** Replace the calling participant's availability set in one transaction.

**Work:**
- `SetMyAvailability` use case; `AvailabilityRepository.ReplaceForParticipant`.
- Request/response DTOs; PUT endpoint with inline validation.
- FE: `setMyAvailability`, `useSaveAvailability` mutation; surface error codes;
  delete localStorage availability modules.

**Acceptance criteria:**
- [x] 200 returns saved block; replaces prior ranges
- [x] 400 invalid header/publicId/participantId or any invalid range (end <= start, > 24h)
- [x] 403 when guest id doesn't match participant
- [x] 404 unknown event/participant/cross-event
- [x] empty ranges clears selection

**Verification:**
- Unit: factory boundary checks; happy path; 403/404; empty clears; invalid range index+reason; idempotent.
- Integration: end-to-end create → join → resolve → PUT → GET; 403; 400 end <= start; 400 > 24h; rewrite wipes prior.

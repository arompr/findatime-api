# 004-events-time-intervals: Proposal

## Goal / Why

Today the availability picker has no notion of granularity: a participant can
submit arbitrary wall-clock bounds, and the organizer's calendar shows whatever
came in. Real scheduling events run on coarse slots — 30 minutes for most
meetings, 15 minutes for a workshop, 60 minutes for a review. Without an
event-level step, participants hand-pick times that do not line up with each
other, and the organizer ends up doing the rounding by hand.

This change introduces **interval** — the calendar step granularity of an
event — as a second event-level param alongside `timezone`. It is:

- **organizer-settable after creation** through the existing full-replace
  params endpoint `PUT /events/{publicId}/params`;
- **default 30 minutes on creation**, with `POST /events` unchanged in shape;
- **constrained to 15 / 30 / 60 minutes**;
- **returned on event read** (`GET /events/{publicId}`) so the frontend can
  render the picker in matching steps without a second round-trip;
- **enforced by the backend** on submitted availability ranges by snapping
  misaligned bounds onto the event's interval grid.

The backend is the owner of grid arithmetic. The frontend renders steps from
the interval it reads and can pre-align its own selections, but the API is
correct even when it does not.

## Scope IN

**Domain (events)** — `EventParams` grows an `IntervalMinutes` value
(`15 | 30 | 60`); `Event.Create` defaults it to `30`; `Event.SetParams`
continues to replace the whole params value object.

**Persistence (events)** — new `events.interval_minutes` integer column, not
null, default `30`; an EF Core migration; `DbContext` maps it as part of the
existing `EventParams` complex property. Existing rows land on `30` via the
column default, so **no data backfill script and no semantic migration of
existing events or existing availability ranges**.

**Application (events)** — `UpdateEventParams` accepts and validates the
interval, rejects values outside `15 / 30 / 60`, and persists it.

**API (events)** — `PUT /events/{publicId}/params` request and response gain
`intervalMinutes`; `GET /events/{publicId}` response gains `intervalMinutes`.

**Read side (events)** — `EventDto`, `ReadEventService`, and
`get_event_by_public_id.sql` surface the interval so `GET /events/{publicId}`
can return it.

**Application (availability)** — `SetAvailability` snaps each submitted range's
`start` down and `end` up onto the event's interval grid **in the submitter's
wall-clock timezone**, before the existing wall-clock → UTC conversion. The
snapped values are what gets stored and echoed back. No `400` is raised for
misalignment.

**Domain (availability)** — the existing `AvailabilityFactory` invariants
(`end > start`, `duration ≤ 24h`) continue to hold on the stored UTC instants;
snapping happens upstream in the use case, as the timezone conversion already
does.

**Cross-cutting** — a single place that owns "allowed interval values" and
"snapping to the grid" (a small domain service or a pure helper, unit-testable
in isolation). If it introduces a new failure mode, a new application
exception and its `error` code are registered in the central
`Common/ExceptionHandler` dictionary.

**Deliverable — frontend handoff document** — a markdown file (this change
folder) containing the API contract facts the frontend developer needs:
new/changed request and response shapes, defaults, allowed values, snapping
behavior, and the error codes. Contract facts only — no frontend planning, no
component design.

**Tests / scratch files** — unit tests for the snapping and interval
validation; integration tests for params persistence, event read, and snapped
availability writes; `events.hurl` entry 4 updated; `availability.hurl` gains
a misalignment case demonstrating the snap.

## Scope OUT

- **No per-participant interval.** The interval is an event-level param only.
- **No change to `POST /events`.** The request body stays
  `{ name, guestId, organizerName, isPasscodeProtected }`; the interval is not
  settable at creation and simply defaults to 30.
- **No interval on `GET /events?guestId=` (search/list).** The brief requires
  the interval on the event read only. The list endpoint returns
  `{ publicId, name, isOrganizer }` and stays that way; the frontend fetches
  the event it is about to render a picker for. Adding it would widen the
  contract without a consumer.
- **No enforcement beyond submitter-frame snapping.** The backend does not
  validate that stored UTC instants fall on the grid, and does not require
  alignment in both the wall-clock frame and the UTC frame. A range in
  `Asia/Kolkata` snapped to a 30-minute grid in wall-clock may land on a
  `:30` UTC instant; that is acceptable and intended.
- **No migration of semantics for existing data.** Existing events get
  interval 30 and keep their availability ranges exactly as stored. Existing
  ranges are **not** re-snapped, re-validated, or re-written. Ranges already in
  the database that do not align to the current interval are still returned
  verbatim by the availability reads.
- **No retroactive re-alignment when the organizer changes the interval.**
  Changing the interval does not touch stored availability. Reads keep
  returning the stored ranges unchanged; the frontend is responsible for
  rendering legacy (possibly non-aligned) ranges.
- **No slot-suggestion / overlap / free-slot-search logic.** The interval is
  display granularity, not a scheduling algorithm.
- **No new endpoint.** The interval rides on the existing params endpoint.
- **No change to the two `QUERY` availability read endpoints' shapes.** They
  are unaffected by this change.

## Decisions handed to spec/design (recommended answer given)

These are the points the brief deliberately left open. Each is stated with a
recommendation so `sdd-spec` can adopt it directly, but each is marked as a
decision to confirm.

**Q1 — Error code for an out-of-set interval value on `PUT /events/{publicId}/params`.**
*Recommendation:* a new dedicated exception and code — `InvalidIntervalException`
→ `400 invalid_interval` — mirroring the existing `InvalidTimezoneException` →
`400 invalid_timezone` precedent. Rationale: the FE must distinguish "your
timezone is wrong" from "your interval is wrong"; reusing
`invalid_availability_range` (which is about ranges, not params) would be
misleading, and `invalid_timezone` is semantically wrong.
*Confirm:* the code string `invalid_interval`.

**Q2 — Is `intervalMinutes` required in the full-replace body?**
*Recommendation:* **required**. `PUT /events/{publicId}/params` is a
full-replace of the params value object; if `intervalMinutes` were optional,
omitting it would have to mean either "keep current" (which breaks
full-replace semantics and makes the operation non-idempotent) or "reset to
30" (a silent surprise). Making it required keeps the operation a pure
full-replace: the body always carries the complete desired param set.
*Confirm:* required; a missing `intervalMinutes` is a request-shape error
(400), consistent with how missing required request fields are handled today.

**Q3 — Can a snapped range become zero-duration, and can it exceed 24 hours?**

*Zero duration:* rounding `start` **down** and `end` **up** can only widen a
range, never narrow it. A range that was valid before snapping (`end > start`)
stays valid after, so it can never become zero-duration. A range whose
`start == end` before snapping is already invalid today and is rejected by
`AvailabilityFactory` as `invalid_availability_range` — that behavior is
unchanged. *Recommendation:* snap only after the existing pre-conditions
(malformed / DST-invalid / end-after-start) are established, and let the
existing factory invariants reject anything that still violates them. Snapping
must not turn a rejected range into an accepted one.

*Exceeding 24 hours:* rounding outward can push a range that was exactly 24h
long (e.g. `09:00–09:00+1d`) past the 24-hour limit, which the DB check
constraint `ck_availabilities_max_duration` would then reject with a
constraint-violation error rather than a clean `400`. *Recommendation:*
**snap first, then validate** — i.e. the snapped bounds are the candidate
values that the existing `AvailabilityFactory` invariants (`end > start`,
`duration ≤ 24h`) are applied to, and a snapped range that violates the 24-hour
limit is rejected with the existing `400 invalid_availability_range`. The
alternative (clamp the end back down to 24h) would silently store a range
different from the grid, which breaks the invariant the interval is supposed
to guarantee. So the accepted contract is: **a snapped range that exceeds 24h
is rejected with `invalid_availability_range`, not silently clamped.**
*Confirm:* rejection, not clamping. This keeps the DB constraint and the
domain invariant in agreement, and the case is rare (only a range already
at/near 24h on a 60-minute grid).

**Q4 — Where does the FE handoff document live?**
*Recommendation:* `Docs/changes/004-events-time-intervals/frontend-handoff.md`,
inside the change folder, so it stays with the change that produced the
contract and is folded into the feature specs (or moved to `Docs/`) at
archive time.

**Q5 — Is `intervalMinutes` returned on `GET /events/{publicId}` only, or also
on the availability read responses?**
*Recommendation:* only on `GET /events/{publicId}` (and on the params
response). The availability read responses describe *stored ranges*, not the
picker's grid, and adding the interval there would invite the FE to
re-interpret already-stored ranges. *Confirm:* availability read shapes stay
unchanged.

## Rationale

- **One param mechanism, not a new one.** `EventParams` was explicitly built
  to grow ("designed to grow more params"), and `PUT /events/{publicId}/params`
  was built as a full-replace for exactly this. Adding a second param is the
  intended use of the existing seam, not a new endpoint.
- **A closed set of values, not a free integer.** Restricting to
  `15 / 30 / 60` keeps the grid meaningful (a 7-minute step is noise) and
  keeps the contract small enough for the frontend to offer as a fixed choice.
  The allowed set is a domain rule, so it belongs in the domain, not in a
  controller.
- **Snap, don't reject.** Rejecting misaligned input would push the burden
  onto every client (the frontend, the TS client, any future integration) and
  would produce a hard failure for a range that is merely off by five minutes.
  Snapping outward (start down, end up) never loses participant intent — it
  only widens it — and the adjusted values are returned in the response, so
  the client can show exactly what was stored instead of guessing.
- **Snap in the submitter's wall-clock frame.** This is the frame the
  participant actually saw in the picker, so "09:05" is rounded to a boundary
  that is meaningful *to them*. It also avoids making the feature unusable for
  participants in non-hour-offset zones: an `Asia/Kolkata` (+05:30) participant
  could never produce a UTC instant aligned to a 60-minute grid, so validating
  in the UTC frame would effectively ban coarse intervals for a large part of
  the world. Snapping before the wall-clock → UTC conversion is therefore
  required for correctness, not merely convenient.
- **No backfill, no re-snapping of existing data.** The column carries a
  non-null default of 30, so existing events are valid immediately. Rewriting
  already-stored ranges would mutate data a participant submitted under a
  different contract and would make the feature's effect depend on when it
  shipped.

## Risks

- **Silent mutation may surprise users.** A participant who submits `09:05–09:50`
  on a 30-minute event gets `09:00–10:00` back, which is 55 minutes wider
  than they asked for. Mitigation: the snapped values are echoed in the
  response so the frontend can display them, and the behavior is documented in
  the FE handoff doc. The alternative (reject) was explicitly declined.
- **The 24-hour interaction.** Outward rounding can push a long range past
  the existing 24-hour invariant (see Q3). Handled by snapping-then-validating
  and rejecting with the existing `invalid_availability_range`, but the FE
  handoff must state that a range near 24h can be rejected after snapping.
- **A full-replace body change is a breaking wire change** for
  `PUT /events/{publicId}/params`: the request now carries a second required
  field, and existing clients that send only `{ timezone }` will get a 400.
  The backend and the regenerated TypeScript client must ship in lockstep.
  *Mitigation:* the TypeScript client is regenerated as part of this change
  (`make typescript-client`), and the FE handoff doc calls out the required
  field explicitly.
- **Interval changes do not retroactively align stored ranges.** An organizer
  who switches an event from 60 to 15 leaves behind ranges that do not align
  to the new grid. Reads return them unchanged. Mitigation: documented as
  expected behavior; the frontend must render stored ranges as-is.
- **The `QUERY` availability reads are absent from the generated TypeScript
  client** (pre-existing OpenAPI limitation). If the frontend relies on
  generated methods for those endpoints, the FE handoff doc must say so.
- **Two independent meanings of "interval"** could leak into the FE (picker
  step vs. duration). Mitigation: one field name, `intervalMinutes`, one
  meaning, documented once in the handoff doc.

## Deliverable (frontend handoff)

`Docs/changes/004-events-time-intervals/frontend-handoff.md` — a
contract-only reference for the frontend developer:

- the `PUT /events/{publicId}/params` request/response shape after this
  change, and that `intervalMinutes` is required and must be 15/30/60;
- the `GET /events/{publicId}` response shape after this change, including
  `intervalMinutes`;
- that `POST /events` is unchanged and new events get 30;
- that submitted ranges are snapped (start down, end up) in the submitter's
  wall-clock timezone, and that the response echoes the snapped values;
- that a snapped range violating `end > start` or the 24-hour maximum is
  rejected with `400 invalid_availability_range`;
- the complete error-code list for the touched endpoints;
- the note that the two `QUERY` availability reads are missing from the
  generated TypeScript client.

# 008-availability-utc-instants: Tasks

Ordered. Each task depends on the ones above it unless marked `[P]`.

Each task gets a `Verified: <iso>` stamp once its **Verification** passes in the
verify phase. A fully landed change carries a `Landed: <iso>` line under its H1.

Environment:

- Run `dotnet --version` first. Prefix commands with
  `DOTNET_ROOT=/home/ar/.dotnet` (and put it first on `PATH`) only if it
  reports 10.x.
- Integration tests need a working Podman/Docker socket (Testcontainers).
- Hurl needs the API on `:5263` against a migrated local DB.

## Task 1 — Snap outward in the event frame

**Depends on:** none

**Goal:** `AvailabilityProjection.SnapOutward` widens one instant range onto the
anchor timezone's grid and returns UTC instants.

**Work:**
- Add `SnapOutward` to `Common/AvailabilityProjection.cs`, reusing `ToWallClock`,
  `ToInstant`, `Interval.Floor`/`Ceil`.
- Delete `Interval.SnapStart/SnapEnd`; retarget their tests in `IntervalTests`
  to `Floor`/`Ceil`.
- Tests in `AvailabilityProjectionTests`: half-hour-offset input on a New York
  60-min grid (13:30–14:30Z → 13:00–15:00Z, and `Project` keeps it), aligned
  pass-through, offset normalization, UTC anchor, NY fall-back hour, range
  spanning the Nov 1 change, invalid interval.

**Acceptance criteria:**
- [x] SC-002 (unit level) and SC-007 hold.
- [x] Output offsets are always zero.

**Verification:**
- `dotnet test tests/FindAtime.UnitTests --filter "AvailabilityProjectionTests|IntervalTests"`

## Task 2 — Use cases drop every timezone

**Depends on:** Task 1

**Goal:** `SetAvailability` takes instants and snaps in the event frame without
touching the participant; reads return projected UTC instants.

**Work:**
- `SetAvailability`: new signature, anchor `Params.Timezone ?? "UTC"`, end >
  start check, `SnapOutward`, `ToUniversalTime`, remove `SetTimezone`, depend on
  `AvailabilityProjection`.
- `GetEventAvailabilities` / `GetParticipantAvailability`: drop the viewer
  timezone and frame logic.
- Result records: drop `Timezone`; `AvailabilityRangeResult` →
  `DateTimeOffset`.
- Delete `TimezoneConverter.ToUtc/ToWallClock` and their tests.
- Integration tests: update the three availability test classes and
  `DeleteEventTests`; add aligned pass-through, offset normalization,
  participant timezone unchanged, half-hour-offset round trip, legacy UTC
  anchor (write and read).

**Acceptance criteria:**
- [x] SC-001 to SC-004 and SC-006 (use-case level) hold.
- [x] `participants.timezone` is unchanged after a save.

**Verification:**
- `dotnet test tests/FindAtime.IntegrationTests --filter "FullyQualifiedName~Availability|DeleteEventTests"`

## Task 3 — HTTP contract

**Depends on:** Task 2

**Goal:** GET reads, `{ ranges }` PUT with offset-bearing instants, no timezone
anywhere.

**Work:**
- `AvailabilityRestService`: `MapGet` for both reads; `ParseInstant` for the
  PUT; remove the timezone check. Delete `AvailabilityQueryRequest.cs`.
- `SetAvailabilityRequest(Ranges)`; responses drop `Timezone`;
  `AvailabilityRangeResponse(DateTimeOffset, DateTimeOffset)`.
- Tests: `ParseInstantTests` (offsets accepted and normalized; offset-less,
  date-only, garbage, empty, null rejected). `OpenApiDocumentTests`: GET without
  body and no `query` op for both paths, `format: date-time` on range
  properties, no `timezone` on availability schemas.
- Rewrite `availability.hurl` for GET reads, instant PUT (Z and `+02:00`),
  offset-less 400, and projection after the interval switch on a Berlin event.

**Acceptance criteria:**
- [x] SC-005 and SC-006 hold at the wire level.
- [x] OpenAPI: both reads are `get`; ranges are `date-time`.
- [x] `hurl --test availability.hurl` passes.

**Verification:**
- `dotnet test`
- `hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl`
- `curl -s http://localhost:5263/events/IKQN3ySKQJFl/availabilities` (SC-008)

## Task 4 — TypeScript client 0.1.0

**Depends on:** Task 3

**Goal:** The generated SDK covers every availability operation.

**Work:**
- `make typescript-client`.
- Delete `typescript-client/src/query.ts` and its export from `src/index.ts`;
  import `getEventAvailabilities`/`getParticipantAvailability` from
  `generated/sdk.gen` in `src/client.ts`.
- Publish 0.1.0 with the `publish-typescript-client` workflow
  (`ref` = the merged branch, `version` = `0.1.0`).

**Acceptance criteria:**
- [x] The client builds; the generated SDK has both GET functions.
- [ ] 0.1.0 is published to GitHub Packages.

**Verification:**
- `make typescript-client`
- `grep -n "getEventAvailabilities\|getParticipantAvailability" typescript-client/src/generated/sdk.gen.ts`
- `npm view @arompr/findatime-typescript-client@0.1.0 version`

## Task 5 — Docs [P]

**Depends on:** Task 3

**Goal:** Feature specs and agent docs describe the instant contract.

**Work:**
- `Docs/specs/availability/{requirements,spec,design}.md`: instant contract,
  GET reads, event-frame snapping, participant timezone set at join only.
- `Docs/specs/events/*`: drop the QUERY paths, "refreshed on availability
  submit", and `SnapStart/SnapEnd`.
- `AGENTS.md`, `CLAUDE.md`: remove the QUERY/wall-clock wording.

**Acceptance criteria:**
- [x] `grep -rn "QUERY\|SnapStart\|wall-clock semantics" AGENTS.md CLAUDE.md Docs/specs` finds nothing stale.

**Verification:**
- `grep -rn "QUERY\|SnapStart\|wall-clock semantics" AGENTS.md CLAUDE.md Docs/specs`

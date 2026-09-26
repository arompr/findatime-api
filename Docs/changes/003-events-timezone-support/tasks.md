# 003-events-timezone-support: Tasks

Ordered. Each task depends on the ones above it unless marked `[P]`
(parallel-safe). Each task ends with a `Verified: <iso>` stamp once its
**Verification** has passed. A fully landed change additionally carries a
`Landed: <iso>` line directly under its H1.

## Task 1 — Common: timezone converter, exception, handler mapping `[P]`

**Depends on:** none

**Goal:** Provide a stateless, injectable `TimezoneConverter` that validates IANA
ids and converts wall-clock ↔ UTC, plus the `InvalidTimezoneException` and its
HTTP mapping, so every later task validates/convert timezones consistently.

**Work:**
- Create `Common/InvalidTimezoneException.cs` — `sealed class` with message
  `"Timezone 'X' is not a valid IANA timezone."`.
- Create `Common/TimezoneConverter.cs` (stateless, no dependencies):
  - `TimeZoneInfo Resolve(string ianaId)` → `TimeZoneInfo.FindSystemTimeZoneById`,
    wrapping `TimeZoneNotFoundException` / `InvalidTimeZoneException` into
    `InvalidTimezoneException`.
  - `DateTimeOffset ToUtc(DateTime wallClock, string ianaId)` → treat `wallClock`
    as `DateTimeKind.Unspecified`, convert via `TimeZoneInfo.ConvertTimeToUtc`;
    throw `InvalidAvailabilityRangeException` when the local time is invalid
    (DST gap) or ambiguous.
  - `DateTime ToWallClock(DateTimeOffset instant, string ianaId)` → convert the
    UTC instant to an `Unspecified` wall-clock `DateTime` in the frame.
- Edit `Common/ExceptionHandler.cs` — add
  `[typeof(InvalidTimezoneException)] = (StatusCodes.Status400BadRequest, "invalid_timezone")`
  to the `Mappings` dictionary (no `if` chain).

**Acceptance criteria:**
- [ ] `Resolve` returns a `TimeZoneInfo` for a valid IANA id (e.g. `Europe/Berlin`).
- [ ] `Resolve` throws `InvalidTimezoneException` for an unknown id.
- [ ] `ToUtc` maps a wall-clock to the correct UTC instant, honoring DST.
- [ ] `ToUtc` throws `InvalidAvailabilityRangeException` for a DST-gap or ambiguous local time.
- [ ] `ToWallClock` maps a UTC instant back to the correct wall-clock in the frame.
- [ ] `ExceptionHandler` maps `InvalidTimezoneException` → 400 `invalid_timezone` with body `{ message, error }`.

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 2 — Domain: EventParams + Event.Params + Participant.SetTimezone + ParticipantFactory timezone `[P]`

**Depends on:** none (parallel-safe with Task 1)

**Goal:** Introduce the `EventParams` value object and the domain write surface
for per-participant timezones, with no behavior change yet (timezone stays null
on the organizer/create path).

**Work:**
- Create `Features/Events/Domain/EventParams.cs` — `public record EventParams(string? Timezone);`
- Edit `Features/Events/Domain/Event.cs` — remove the
  `[Column("timezone")] string? Timezone` property; add
  `public EventParams Params { get; private set; }` initialized to
  `new EventParams(null)` (in the public constructor); add
  `public void SetParams(EventParams @params)`.
- Edit `Features/Events/Domain/Participant.cs` — add
  `public void SetTimezone(string? timezone)` (the `Timezone` property keeps its
  existing setter/column).
- Edit `Features/Events/Domain/ParticipantFactory.cs` — add a
  `string? timezone = null` parameter to `CreateParticipant(...)` and set it on
  the new `Participant` (via `SetTimezone`). The optional default keeps
  `EventFactory` valid (organizer timezone remains null at create).

**Acceptance criteria:**
- [ ] `Event` exposes `Params` (not a bare `Timezone`) and defaults to `new EventParams(null)` (option off).
- [ ] `Event.SetParams` replaces the whole `Params` value object.
- [ ] `Participant.SetTimezone` sets the stored timezone.
- [ ] `ParticipantFactory.CreateParticipant(..., timezone)` records the supplied timezone; the organizer/create path still compiles with timezone null.
- [ ] No remaining references to `Event.Timezone` anywhere in `src/`.

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 3 — EF write mapping: DbContext complex property for EventParams

**Depends on:** Task 2

**Goal:** Persist `EventParams.Timezone` onto the existing `events.timezone`
column (no migration) via a complex-property mapping.

**Work:**
- Edit `Features/Events/Infra/DbContext.cs` in `modelBuilder.Entity<Event>`: add
  `entity.ComplexProperty(e => e.Params, c => c.Property(p => p.Timezone).HasColumnName("timezone"));`
  (replacing the column mapping the removed `[Column("timezone")]` attribute
  used to provide).

**Acceptance criteria:**
- [ ] `EventParams.Timezone` maps to the existing `events.timezone` column (named `timezone`, not `Params_Timezone`).
- [ ] No schema delta: `dotnet ef migrations has-pending-model-changes --project src/FindAtime.Api/FindAtime.Api.csproj` reports no changes.

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`
- `dotnet ef migrations has-pending-model-changes --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 4 — Events read infra: expose event timezone `[P]`

**Depends on:** none (parallel-safe with Task 3)

**Goal:** Surface the event's `timezone` on the read side (`EventDto`) so
availability reads and `GetEvent` can use it as the fallback frame.

**Work:**
- Edit `Features/Events/Infra/Read/get_event_by_public_id.sql` —
  `SELECT public_id, name, timezone, (passcode_hash IS NOT NULL) AS is_passcode_protected`.
- Edit `Features/Events/Infra/Read/EventDto.cs` —
  `record EventDto(string PublicId, string Name, string? Timezone, bool IsPasscodeProtected)`.
- Edit `Features/Events/Infra/Read/ReadEventService.cs` `GetEventByPublicId` —
  read `timezone` via `IsDBNull → null`.

**Acceptance criteria:**
- [ ] `EventDto.Timezone` is populated for events with a set timezone and null otherwise.
- [ ] The existing `SearchEvents` path is unaffected.

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 5 — Events: capture timezone on join (application + API)

**Depends on:** Task 1, Task 2

**Goal:** Make `join` always record the participant's IANA timezone (required),
validating it and rejecting unknown ids.

**Work:**
- Edit `Features/Events/Application/JoinEvent.cs` — add a `string timezone`
  parameter to `Execute`; inject `TimezoneConverter` via constructor; after the
  duplicate-join guard call `_timezoneConverter.Resolve(timezone)` (throws
  `InvalidTimezoneException`); then `participant.SetTimezone(timezone)`.
- Edit `Features/Events/Api/JoinEventRequestParams.cs` —
  `record(string? Passcode, string GuestId, string ParticipantName, string Timezone)`.
- Edit `Features/Events/Api/EventsRestService.cs` join endpoint — add inline
  `if (string.IsNullOrWhiteSpace(requestParams.Timezone)) return Results.BadRequest("timezone is required");`
  and pass `requestParams.Timezone` to `joinEvent.Execute`.

**Acceptance criteria:**
- [ ] Join records the participant timezone even when the event timezone option is off.
- [ ] Blank timezone → 400 `timezone is required` (inline BadRequest).
- [ ] Unknown IANA id → 400 `invalid_timezone` (via `InvalidTimezoneException`).
- [ ] The duplicate-join guard still fires before any timezone work.

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 6 — Events: update-event-params endpoint + GetEventResponse.Timezone (application + API)

**Depends on:** Task 1, Task 2, Task 3, Task 4

**Goal:** Add the organizer-only toggle endpoint `PUT /events/{publicId}/params`
(full-replace `EventParams`) and return the event timezone on
`GET /events/{publicId}`.

**Work:**
- Create `Features/Events/Application/UpdateEventParams.cs` —
  `Execute(string publicId, Guid guestId, string? timezone)`:
  1. `EventRepository.GetByPublicId(publicId)` (includes participants) → 404
     `EventNotFoundException`.
  2. Resolve the organizer via `OrganizerParticipantId` + `FindParticipant`; if
     `GuestId != GuestId.FromString(guestId.ToString())` throw `ForbiddenException`.
  3. If `timezone` is non-null, `TimezoneConverter.Resolve(timezone)`.
  4. `event.SetParams(new EventParams(timezone))`; `EventRepository.Save`;
     return `new UpdateEventParamsResponse(timezone)`.
- Create `Features/Events/Api/UpdateEventParamsRequestParams.cs` —
  `record(string? Timezone)`.
- Create `Features/Events/Api/UpdateEventParamsResponse.cs` —
  `record(string? Timezone)`.
- Edit `Features/Events/Api/GetEventResponse.cs` —
  `record(string PublicId, string Name, bool IsPasscodeProtected, string? Timezone)`.
- Edit `Features/Events/Api/EventsRestService.cs`:
  - `GET /events/{publicId}` — include `dto.Timezone` in the `GetEventResponse`.
  - Add `PUT /events/{publicId}/params` — read `X-Guest-Id` header
    (blank/malformed → inline `Results.BadRequest`), call `UpdateEventParams`,
    return `200` with `UpdateEventParamsResponse`; `.Produces(200, 400, 403, 404)`.

**Acceptance criteria:**
- [ ] Organizer can enable/change/disable the timezone option; a non-organizer gets 403 `forbidden`.
- [ ] Unknown IANA id → 400 `invalid_timezone`; `null` timezone disables (option off).
- [ ] `GET /events/{publicId}` returns the event `timezone` (null when off).
- [ ] The endpoint is idempotent (full-replace).

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 7 — Availability: timezone-aware write + viewer-timezone read (application + API + DTOs)

**Depends on:** Task 1, Task 2, Task 4

**Goal:** Store availability as canonical UTC instants from wall-clock input in
the participant's timezone, and render reads as wall-clock in the resolved
viewer frame (with the timezone label), honoring the off→UTC legacy path.

**Work:**
- Application:
  - Edit `Features/Availability/Application/SetAvailability.cs` — add a
    `string timezone` parameter; after the guest-id check,
    `TimezoneConverter.Resolve(timezone)` + `participant.SetTimezone(timezone)`;
    for each range parse wall-clock `DateTime` (`DateTimeKind.Unspecified`),
    `ToUtc(..., timezone)` → factory; return `SetAvailabilityResult` with ranges
    converted back via `ToWallClock(..., timezone)` + the `timezone` label.
  - Edit `Features/Availability/Application/GetEventAvailabilities.cs` — add
    `string? viewerTimezone`; compute `frame = viewerTimezone ?? dto.EventTimezone ?? "UTC"`,
    forced to `"UTC"` when the event timezone is null (option off); map each raw
    UTC range via `ToWallClock(instant, frame)`; return `GetEventAvailabilitiesResult(frame, ...)`.
  - Edit `Features/Availability/Application/GetParticipantAvailability.cs` — add
    `string? viewerTimezone`; read the event timezone from `EventDto.Timezone`,
    resolve the frame the same way, convert ranges, return
    `ParticipantAvailabilityResult(..., frame, ...)`.
  - Edit `Features/Availability/Application/GetEventAvailabilitiesResult.cs` /
    `SetAvailabilityResult.cs` (incl. `ParticipantAvailabilityResult` /
    `AvailabilityRangeResult`) — `AvailabilityRangeResult(DateTime Start, DateTime End)`;
    add `string Timezone` to `GetEventAvailabilitiesResult` (replacing
    `EventTimezone`), `SetAvailabilityResult`, and `ParticipantAvailabilityResult`.
- API:
  - Edit `Features/Availability/Api/SetAvailabilityRequest.cs` —
    `record SetAvailabilityRequest(string Timezone, IReadOnlyList<AvailabilityRangeRequest> Ranges)`;
    `record AvailabilityRangeRequest(string Start, string End)`.
  - Edit `Features/Availability/Api/SetAvailabilityResponse.cs` —
    `record(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResponse> Ranges)`.
  - Edit `Features/Availability/Api/GetEventAvailabilitiesResponse.cs` — top-level
    `string Timezone` (replaces `string? EventTimezone`); `AvailabilityRangeResponse(DateTime Start, DateTime End)`.
  - Create `Features/Availability/Api/GetParticipantAvailabilityResponse.cs` —
    `record(string ParticipantId, string Name, string Timezone, IReadOnlyList<AvailabilityRangeResponse> Ranges)`.
  - Create `Features/Availability/Api/AvailabilityQueryRequest.cs` —
    `record(string? Timezone)`.
  - Edit `Features/Availability/Api/AvailabilityRestService.cs`:
    - `PUT …/availability` — inline blank `timezone` check; map the new
      request/response DTOs; pass `timezone` to `SetAvailability`.
    - Replace the two GET reads with `QUERY` endpoints on their original paths,
      registered via `app.MapMethods(pattern, [HttpMethods.Query], handler)` with
      `[FromBody] AvailabilityQueryRequest`; validate a non-null `Timezone` via
      `TimezoneConverter.Resolve` (invalid → 400 `invalid_timezone`), else pass null.

**Acceptance criteria:**
- [ ] Submitting wall-clock ranges in a participant timezone stores the correct UTC instants (end-after-start / ≤24h measured on UTC).
- [ ] A malformed, offset-bearing, ambiguous, or DST-gap range → 400 `invalid_availability_range`; unknown timezone → 400 `invalid_timezone`.
- [ ] With the option off, reads return UTC wall-clock with label `UTC` (legacy, no conversion).
- [ ] With the option on, participant B sees participant A's 18:00–20:00 (tz X) as the correct local times in tz Y, with the resolved label.
- [ ] Viewer frame falls back viewer body timezone → event timezone → UTC.
- [ ] The two read endpoints are reachable via `QUERY` on their original paths.

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 8 — Composition/DI: register converter + UpdateEventParams

**Depends on:** Task 1, Task 5, Task 6, Task 7

**Goal:** Register the new stateless converter and the new use case, so the
application resolves them at runtime.

**Work:**
- Edit `Composition/ServiceCollectionExtensions.cs`:
  - `services.AddSingleton<TimezoneConverter>();`
  - `services.AddScoped<UpdateEventParams>();`
  - `JoinEvent`, `SetAvailability`, `GetEventAvailabilities`,
    `GetParticipantAvailability` already declare the `TimezoneConverter`
    constructor parameter (added in Tasks 5/7); DI now resolves it.

**Acceptance criteria:**
- [ ] `TimezoneConverter` resolves as a singleton; `UpdateEventParams` resolves as scoped.
- [ ] `JoinEvent`, `SetAvailability`, `GetEventAvailabilities`, `GetParticipantAvailability` receive `TimezoneConverter`.

**Verification:**
- `dotnet build --project src/FindAtime.Api/FindAtime.Api.csproj`

## Task 9 — Tests: update and add unit + integration coverage

**Depends on:** Task 1–Task 8

**Goal:** Update existing tests for the new signatures/wire shapes and add
coverage for conversion, the params endpoint, and viewer-timezone rendering.

**Work:**
- Create `tests/FindAtime.UnitTests/Common/TimezoneConverterTests.cs` — Resolve
  valid/invalid; `ToUtc` across DST; `ToUtc` throws on gap/ambiguous; `ToWallClock`
  round-trip.
- Edit `tests/FindAtime.UnitTests/Domain/EventTests.cs` — assert `Params` defaults
  to null and `SetParams` replaces; update any `Timezone` references.
- Edit `tests/FindAtime.UnitTests/Domain/ParticipantFactoryTests.cs` — assert the
  factory records the timezone.
- Edit `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs` — register
  `TimezoneConverter` (singleton) and `UpdateEventParams` (scoped) in the manual
  DI setup.
- Edit `tests/FindAtime.IntegrationTests/Application/Events/JoinEventTests.cs` —
  pass a `timezone` to `JoinEvent.Execute`; add a case for unknown IANA →
  `InvalidTimezoneException`.
- Create `tests/FindAtime.IntegrationTests/Application/Events/UpdateEventParamsTests.cs` —
  organizer enable/change/disable, non-organizer 403, unknown event 404, invalid
  timezone 400.
- Edit `tests/FindAtime.IntegrationTests/Application/Availability/SetAvailabilityTests.cs` —
  switch to wall-clock + timezone; assert stored UTC and returned wall-clock.
- Edit `tests/FindAtime.IntegrationTests/Application/Availability/GetEventAvailabilitiesTests.cs`
  and `GetParticipantAvailabilityTests.cs` — assert `UTC` label when off,
  converted times + label when on, and the viewer→event→UTC fallback chain.

**Acceptance criteria:**
- [ ] All pre-existing tests pass under the new contracts.
- [ ] New tests assert: valid/invalid IANA resolution; DST-gap rejection; option-off returns UTC label; option-on converts across timezones; params-endpoint authorization; fallback chain.

**Verification:**
- `dotnet test`
- `dotnet test --filter TimezoneConverter` (focused)

## Task 10 — TypeScript client regeneration

**Depends on:** Task 9 (full solution green)

**Goal:** Regenerate the generated FE client so request/response models and the
new/renamed endpoints (QUERY reads, `PUT /events/{publicId}/params`, wall-clock +
timezone shapes) are reflected.

**Work:**
- Run `make typescript-client` (builds `src/FindAtime.Api/FindAtime.Api.csproj -c Release`,
  then `npm install && npm run prepare` in `typescript-client`).
- Commit the regenerated `typescript-client/dist/generated/**` (new models for
  `UpdateEventParamsRequestParams`/`Response`, `AvailabilityQueryRequest`,
  `GetParticipantAvailabilityResponse`, updated `SetAvailabilityRequest`/`Response`,
  `GetEventAvailabilitiesResponse`, `GetEventResponse`, `JoinEventRequestParams`).

**Acceptance criteria:**
- [ ] Generated client contains the new/renamed models and endpoint methods.
- [ ] No stale `DateTimeOffset`-typed availability fields remain in the generated models.
- [ ] `npm run prepare` completes without errors.

**Verification:**
- `make typescript-client`
- Inspect `typescript-client/dist/generated/**` for the new models/endpoints.

---

**Fold target:** this change folds into BOTH `Docs/specs/events/` and
`Docs/specs/availability/`.

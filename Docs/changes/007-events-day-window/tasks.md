# 007-events-day-window: Tasks

Ordered. Each task depends on the ones above it unless marked `[P]`
(parallel-safe).

Each task ends with a `Verified: <iso>` stamp once its **Verification** has
passed. A fully landed change additionally carries a `Landed: <iso>` line
directly under its H1.

Environment:

- Run `dotnet --version` first. Prefix commands with
  `DOTNET_ROOT=/home/ar/.dotnet` only if it reports 10.x.
- Integration tests need a working Podman/Docker socket (Testcontainers).
- The API must be running on `:5263` against a migrated local DB for Hurl.

## Task 1 — Day window in the domain

**Depends on:** none

**Goal:** `EventParams` carries a validated whole-hour day window, and new events
default to 09:00–21:00.

**Work:**
- Create `src/FindAtime.Api/Features/Events/Domain/DayWindow.cs` (static:
  `DefaultStartHour = 9`, `DefaultEndHour = 21`, `IsValid`, `Validate`).
- Create `src/FindAtime.Api/Features/Events/Domain/InvalidDayWindowException.cs`.
- Edit `EventParams.cs`: add `DayStartHour`/`DayEndHour` and the 4-arg constructor
  validating the interval and then the window.
- Edit `Event.Create` to pass the defaults.
- Edit `Common/ExceptionHandler.cs`: map `InvalidDayWindowException` →
  `(400, "invalid_day_window")`.
- Edit `UpdateEventParams.cs` so it compiles: pass the event's current window
  (`domainEvent.Params.DayStartHour/DayEndHour`) through. Task 3 replaces this.
- Tests: new `tests/FindAtime.UnitTests/Domain/DayWindowTests.cs`. Extend
  `EventTests` (defaults 9/21, `SetParams` with a window, constructor throws
  `InvalidDayWindowException`, interval error wins over window error). Fix the
  existing `new EventParams(...)` call sites in tests.

**Acceptance criteria:**
- [x] `new EventParams(tz, interval, s, e)` throws `InvalidDayWindowException`
      unless `0 <= s < e <= 24`.
- [x] An invalid interval throws `InvalidIntervalException` even when the window
      is also invalid.
- [x] `Event.Create` yields `DayStartHour = 9`, `DayEndHour = 21`.
- [x] Solution builds; all unit tests pass.

**Verification:**
- `dotnet build`
- `dotnet test tests/FindAtime.UnitTests --filter "DayWindowTests|EventTests|IntervalTests"`

## Task 2 — Persistence and migration

**Depends on:** Task 1

**Goal:** The window persists in `events` with DB defaults 9/21 and a check
constraint.

**Work:**
- Edit `Features/Events/Infra/DbContext.cs`: map `DayStartHour` →
  `day_start_hour` and `DayEndHour` → `day_end_hour` in the `Params` complex
  property, with `.HasDefaultValue(DayWindow.Default*)` and `.IsRequired()`. Add
  the `ck_events_day_window` check constraint on the `events` table.
- `dotnet ef migrations add AddEventDayWindow --project src/FindAtime.Api/FindAtime.Api.csproj`.
  Review the generated `Up`/`Down` and the model snapshot.
- **Stop and ask the user** before running `dotnet ef database update` against
  any database (Testcontainers applies migrations itself).
- Tests: extend `tests/FindAtime.IntegrationTests/Infrastructure/EventRepositoryTests.cs`
  (window round-trips through save/load; a new event persists 9/21).

**Acceptance criteria:**
- [x] Migration adds two `integer NOT NULL` columns with defaults 9 and 21 and
      the check constraint, and `Down` drops them.
- [x] A saved event reloads with the same window.
- [x] Integration tests pass.

**Verification:**
- `dotnet build`
- `dotnet test tests/FindAtime.IntegrationTests --filter "EventRepositoryTests|CreateEventTests"`

## Task 3 — Params endpoint: replace and echo the window

**Depends on:** Task 2

**Goal:** The organizer sets the window through `PUT /events/{publicId}/params`.
The fields are required and validated, and the response echoes them.

**Work:**
- Edit `UpdateEventParams.Execute` to take `dayStartHour`/`dayEndHour`, build
  `EventParams` from them (replacing the Task 1 pass-through), and return all four
  values.
- Edit `UpdateEventParamsRequestParams` (add `int? DayStartHour`, `int? DayEndHour`)
  and `UpdateEventParamsResponse` (add `int DayStartHour`, `int DayEndHour`).
- Edit `EventsRestService` params endpoint: inline 400 `"dayStartHour is required"`
  / `"dayEndHour is required"` after the `intervalMinutes` check, and pass the
  values through.
- Tests: extend `tests/FindAtime.IntegrationTests/Application/Events/UpdateEventParamsTests.cs`
  with SC-002 to SC-006 and SC-008 to SC-010, including "window unchanged after a
  rejected update". Update existing calls to the new signature.

**Acceptance criteria:**
- [x] Valid window replaces and echoes, and a reload shows it.
- [x] Inverted, empty, or out-of-range window → `InvalidDayWindowException`;
      stored params unchanged.
- [x] Interval is validated before the window, and the window before the timezone.
- [x] Non-organizer → `ForbiddenException`; missing event → `EventNotFoundException`.
- [x] Integration tests pass.

**Verification:**
- `dotnet build`
- `dotnet test tests/FindAtime.IntegrationTests --filter "UpdateEventParamsTests"`

## Task 4 — Event read returns the window

**Depends on:** Task 3 (shares `EventsRestService.cs`)

**Goal:** `GET /events/{publicId}` returns `dayStartHour` and `dayEndHour`.

**Work:**
- Edit `Features/Events/Infra/Read/get_event_by_public_id.sql` to select
  `day_start_hour, day_end_hour`.
- Edit `EventDto` and `ReadEventService.GetEventByPublicId` to read both.
- Edit `GetEventResponse` and the GET endpoint mapping.
- Tests: extend `tests/FindAtime.IntegrationTests/Application/Events/GetEventTests.cs`
  (new event → 9/21; after an update → the new values).

**Acceptance criteria:**
- [x] The read returns the stored window, and a new event returns 9/21.
- [x] `search_events.sql` and the list response are unchanged.
- [x] Full test suite passes (availability tests unchanged and green).

**Verification:**
- `dotnet build`
- `dotnet test`

## Task 5 — Wire scratch file and TypeScript client

**Depends on:** Task 4

**Goal:** Prove the contract at the wire level and ship it to the frontend client.

**Work:**
- Edit `src/FindAtime.Api/Features/Events/Api/events.hurl`:
  - entry 3: assert `$.dayStartHour == 9` and `$.dayEndHour == 21`;
  - entry 4: send and assert `dayStartHour: 9, dayEndHour: 17`;
  - insert a new entry 5: PUT params with `dayStartHour: 17, dayEndHour: 9` →
    400, `$.error == "invalid_day_window"`;
  - renumber the following entries and update the header list.
- Edit `src/FindAtime.Api/Features/Availability/Api/availability.hurl` entry 7
  (PUT params) to send `dayStartHour`/`dayEndHour`, otherwise it fails with
  400 on the new required fields.
- `make typescript-client`. Check that `dayStartHour`/`dayEndHour` appear on
  `GetEventResponse`, `UpdateEventParamsRequestParams`, and
  `UpdateEventParamsResponse`.
- Copy `frontend-handoff.md` in this folder into the frontend repo, or point the
  frontend developer to it.

**Acceptance criteria:**
- [ ] `hurl --test events.hurl` passes against a migrated local DB.
- [x] The generated client contains both fields on the three models.
- [x] `OpenApiDocumentTests` still pass.

**Verification:**
- `hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl`
- `dotnet test tests/FindAtime.UnitTests --filter "OpenApiDocumentTests"`
- `grep -n "dayStartHour" typescript-client/src -r`

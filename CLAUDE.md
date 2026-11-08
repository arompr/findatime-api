# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@AGENTS.md

AGENTS.md (imported above) is the shared source of project conventions and
commands. The notes below record where the code has moved past AGENTS.md, plus
things that take reading several files to work out. Where they conflict, these
notes win.

## Where AGENTS.md is out of date

- **Target framework is `net11.0`** (all three csproj files), not net10. CI
  (`.github/workflows/deploy-staging.yml`) uses .NET 11 and `dotnet-ef`
  11.0.0-rc. Older change docs prefix commands with
  `DOTNET_ROOT=/home/ar/.dotnet` because the `dotnet` on PATH used to be SDK 10.
  Run `dotnet --version` first; you only need the prefix if it reports 10.x.
- **DI lives in `Composition/ServiceCollectionExtensions.cs`**
  (`AddFindAtimeServices`), not inline in `Program.cs`. `Program.cs` only wires
  config (`.env` files), CORS, OpenAPI, the exception handler, and the
  `Map<Feature>()` endpoint groups.
- **Integration tests have their own copy of the DI graph** in
  `tests/FindAtime.IntegrationTests/Fixtures/IntegrationTest.cs`. A new
  use case, repository, or service has to be registered in both places.

## Testing

- Integration tests run against a real `postgres:18` from Testcontainers
  (`PostgresFixture`, shared through the `"postgres"` xunit collection). Each
  test runs inside one transaction that is rolled back on dispose, and EF and
  the Dapper read side share that connection through
  `TransactionalReadConnectionProvider`. A test sees its own uncommitted writes
  and leaves no data behind. Testcontainers needs a working Podman/Docker socket.
- Integration tests resolve use cases straight from the service provider. They
  do not go over HTTP; wire-level checks are done with the Hurl files.
- Run a single test:
  `dotnet test --filter "FullyQualifiedName~SetAvailabilityTests"`. Run one
  project with `dotnet test tests/FindAtime.UnitTests`.
- `tests/FindAtime.UnitTests/OpenApiDocumentTests.cs` checks the generated
  OpenAPI document. When a response shape changes, regenerate the TypeScript
  client with `make typescript-client` (it builds Release and runs the npm
  prepare step in `typescript-client/`).

## Cross-cutting code in `Common/`

Time handling is shared by both features and is the subtlest code in the repo:
`Interval` (grid snapping: `Floor`/`Ceil`/`SnapStart`/`SnapEnd`),
`TimezoneConverter`, and `AvailabilityProjection`. `AvailabilityProjection`
merges participants' ranges into a union and projects it onto the event's
interval grid in the event's anchor timezone. It must handle DST gaps and
ambiguous times without throwing. Read `Docs/specs/availability/design.md`
before changing any of these.

## When to use SDD

SDD is opt-in. Use it when planning or building an entire new feature, or a
change big enough to need its own `Docs/changes/NNN-*` folder, or when the user
asks for it. Most edits are not that: bug fixes, refactors, small tweaks to
existing behaviour, test or tooling work. Make those directly, with no change
folder and no phase gates. The spec-driven workflow described in AGENTS.md
applies only to SDD work.

A direct edit still has to keep the feature docs accurate. If it changes
behaviour or a contract described in `Docs/specs/<feature>/`, update the
matching `spec.md`/`design.md` and the feature's `.hurl` file in the same edit.
If you can't tell whether a request should go through SDD, ask.

## SDD workflow in practice (opencode `sdd` agents)

SDD changes are produced with a user-level opencode setup
(`~/.config/opencode/agent/sdd*.md` + `~/.config/opencode/skills/sdd/`). It is
more detailed than the 3-file change flow in AGENTS.md. Recent changes
(e.g. `Docs/changes/006-*`) follow it, so follow it too when you create or
continue a change:

```
explore → proposal → spec → design → tasks → apply → verify → archive
research.md  proposal.md  spec.md  design.md  tasks.md  (code + [x])  verification.md + Verified:  folded specs + Landed:
```

- **Change folder** `Docs/changes/NNN-kebab-slug/`. NNN is the highest existing
  3-digit prefix + 1. Each phase writes only its own file and may start only
  once the previous phase's file exists.
- **Templates:** use the local `Docs/specs/_templates/changes/<noun>.md` when it
  exists (currently `requirements`, `design`, `tasks`). Otherwise use
  `~/.config/opencode/skills/sdd/templates/changes/<noun>.md` (`research`,
  `proposal`, `spec`, `verification`, …). Fill every `<…>` placeholder.
- **`spec.md`** (change): `REQ-*` requirements in WHEN/THEN form, `SC-*`
  scenarios in GIVEN/WHEN/THEN form, and a flat list of `- [ ]` acceptance
  criteria. It stays at the WHAT level. Its checkboxes are never progress
  markers.
- **`tasks.md` is the only progress signal.** Tasks are ordered, `[P]` marks a
  parallel-safe task, and each task has **Depends on / Goal / Work / Acceptance
  criteria / Verification**. Tests ship in the same task as the behaviour they
  cover; never write a test-only task.
- **apply**: implement one task, build, run its tests, flip its box to `- [x]`.
  Do not write `Verified:`. Do not edit spec, design, or `Docs/specs/`. Stop and
  ask before any destructive verification step (migrations, deploys).
- **verify**: re-run every task's Verification commands independently and write
  `verification.md` with `**Status:** PASS|FAIL`. On a pass, append
  `Verified: <ISO-8601 UTC>` under each task. Never edit code during verify.
- **archive**: only once every task is `- [x]` and stamped and verification
  passed. Fold the deltas into `Docs/specs/<feature>/{requirements,spec,design}.md`,
  then add `Landed: <ISO-8601 UTC>` under the H1 of `tasks.md`. A `Landed:`
  change is terminal: never edit or re-run it.
- **Ask, don't guess:** when scope, naming, or a design choice is ambiguous,
  stop and ask the user with labelled options and one marked as recommended.
  Ask for approval before moving from one phase to the next.

# Documentation Index

This repo follows spec-driven development. This index is the entry point for
what the product does today and where each part is documented.

## Features

Each feature folder holds three files — `requirements.md` (enduring product
intent), `spec.md` (authoritative current behavior), and `design.md`
(authoritative current architecture).

- [Events](specs/events/) — create a shareable event and become its organizer;
  participants join (optionally behind a passcode) or leave, and are identified
  by a per-browser guest id.
- [Availability](specs/availability/) — participants mark the time ranges they
  are free; organizers view everyone's availability to pick a slot.

## Workflow

- [Spec templates](specs/_templates/) — the templates for `requirements.md`,
  `spec.md`, and `design.md`, plus the change templates.
- [Changes](changes/) — historical/temporary work, one numbered folder per
  change. A change moves a feature from one state to the next via
  `requirements.md` → `design.md` → `tasks.md`, then is folded back into the
  feature's spec files.

## Reference

- [Database migrations](db-migrations.md) — how to evolve the schema and apply
  migrations.

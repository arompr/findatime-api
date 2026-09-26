# 003-events-timezone-support: Requirements

## Goal

Activate the existing timezone scaffolding (`timezone` columns on events and
participants, `EventTimezone` read field — all null today) so that availability
is stored as one canonical absolute instant per range, but entered and displayed
in each participant's own local timezone. The backend does all timezone
conversion; the frontend renders wall-clock times it receives without doing
timezone math itself.

## Scope

In scope:
- Always recording each participant's timezone (the frontend always supplies
  it), independent of any event-level setting, and interpreting the availability
  ranges they submit as local wall-clock times in that timezone.
- An event-level timezone option that is off by default and can be toggled
  on/off after creation (event params); enabling it switches availability reads
  to timezone-aware rendering, and may provide a default/fallback display frame.
- Storing every range as one canonical absolute instant pair (start/end),
  independent of the submitter's timezone.
- Returning availability ranges converted into the viewer's timezone when the
  timezone option is on, so two participants in different timezones see the same
  availability as different local times.

Out of scope:
- The frontend itself (this change only guarantees the backend contract the
  frontend needs).
- The exact wire representation (IANA id vs UTC offset) — a design decision.
- Scheduling/slot-suggestion logic beyond storing and returning availability.
- Changing existing availability invariants (end after start, ≤ 24 hours).

## User Stories

### US-001: Optional, toggleable event timezone option
**As an** organizer
**I want to** turn timezone-aware display on or off for my event (and set a
default frame) after creation
**So that** I can opt into timezone-aware display when I need it

**Acceptance Criteria:**
WHEN I create an event THEN THE SYSTEM SHALL leave the timezone option off by default
WHEN I enable the timezone option THEN THE SYSTEM SHALL record it and render availability in viewers' timezones
WHEN I disable or clear the timezone option THEN THE SYSTEM SHALL fall back to legacy (no-conversion) behavior

### US-002: Each participant's timezone is always captured
**As a** participant
**I want to** have my timezone recorded with my participation, always
**So that** the availability times I enter are always interpreted as my local time, and enabling the event's timezone option later works without any re-entry

**Acceptance Criteria:**
WHEN I join or submit availability THEN THE FRONTEND SHALL always supply my timezone and the system SHALL record it, regardless of the event's timezone option
WHEN my timezone differs from another participant's THEN THE SYSTEM SHALL convert my ranges when they view them (once the option is on)

### US-003: Availability is stored as absolute instants
**As a** participant
**I want to** submit my free times once in my own local time
**So that** the system can show the same availability to everyone in their own timezone

**Acceptance Criteria:**
WHEN I submit availability ranges THEN THE SYSTEM SHALL store each range as one canonical absolute start and end
WHEN a submitted range cannot be interpreted unambiguously THEN THE SYSTEM SHALL reject the request

### US-004: Availability is displayed in the viewer's timezone
**As a** participant or organizer
**I want to** read everyone's availability in my own timezone
**So that** I see times that are correct for me, regardless of where other participants are

**Acceptance Criteria:**
WHEN the timezone option is on and participant A (timezone X) enters 18:00–20:00 THEN participant B (timezone Y) SHALL see that same availability converted to Y's local time
WHEN the frontend reads availability with the option on THEN THE SYSTEM SHALL return each range as wall-clock times in the viewer's timezone together with the timezone label
WHEN the viewer has no timezone recorded THEN THE SYSTEM SHALL fall back to the event's timezone, then to UTC

## Functional Requirements

### FR-001: Optional, toggleable event timezone option
**Priority:** P0
**Persona:** organizer

WHEN an event is created THEN THE SYSTEM SHALL leave the timezone option off; an
organizer SHALL be able to enable, change, or disable it afterwards.

### FR-002: Per-participant timezone always captured
**Priority:** P0
**Persona:** participant

WHEN a participant joins or submits availability THEN the frontend SHALL always
supply the participant's timezone and the system SHALL record it, regardless of
the event's timezone option, so the option can be enabled later with no backfill.

### FR-003: Canonical absolute storage
**Priority:** P0
**Persona:** system

WHEN availability is submitted THEN THE SYSTEM SHALL store each range as a single
canonical absolute start and end, independent of the submitter's timezone.

### FR-004: Viewer-timezone rendering
**Priority:** P0
**Persona:** organizer

WHEN the timezone option is on and availability is read THEN THE SYSTEM SHALL
convert each range into the viewer's timezone (falling back to the event
timezone, then UTC) and return the timezone label alongside the ranges. When the
option is off, availability is read without conversion (legacy behavior).

## Non-Functional Requirements

### NFR-001: Timezone transparency
The backend is the source of truth for the absolute instant and for conversion.
The frontend shows times in the viewer's own timezone without reinterpreting or
converting the instants itself.

### NFR-002: Unambiguous values on the wire
Every time range the backend sends or receives identifies its absolute start and
end without guesswork; the backend never relies on implicit assumptions about
which timezone a value belongs to.

### NFR-003: Backward compatibility without backfill
Because the participant's timezone is always captured from day one, enabling the
event's timezone option later requires no migration or data re-entry. Events and
participants that already exist with a null timezone remain valid; reads
continue to work with the option off and return unambiguous instants.

## Out of Scope

- The frontend: planning or changing how it renders or sends data.
- Prescribing the wire format for timezones (IANA identifier vs UTC offset) —
  deferred to design.
- Any scheduling logic such as suggesting a common free slot or analyzing
  overlaps across timezones.
- Changing existing availability validation invariants (end after start, maximum
  24-hour duration).
- Real authentication beyond the public id + guest id trust model.

## Acceptance Criteria

- [ ] An event is created with the timezone option off by default; the organizer can enable, change, or disable it afterwards.
- [ ] The participant's timezone is always captured (frontend always supplies it), even when the event's timezone option is off.
- [ ] Availability submitted in a participant's timezone is stored as the correct absolute instants.
- [ ] With the option on, availability is returned converted into the viewer's timezone, so participant B sees participant A's 18:00–20:00 (tz X) as the correct local times in tz Y.
- [ ] Reads return the timezone label alongside the ranges, and fall back event timezone → UTC when the viewer has none.
- [ ] With the option off, reads return raw instants (legacy behavior).
- [ ] A submitted range that cannot be interpreted unambiguously is rejected.
- [ ] Enabling the option later requires no backfill; existing null-timezone participants still work.

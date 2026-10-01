# Events Requirements

Events are the core of Findatime: an organizer creates a shareable event,
participants join it (optionally behind a passcode), and everyone is identified
by a per-browser guest id rather than a real account.

## User Stories

### US-001: Create an event
**As an** organizer
**I want to** create an event with a name and, optionally, a passcode
**So that** I can share a link for others to join

**Acceptance Criteria:**
WHEN I create an event THEN THE SYSTEM SHALL return a shareable public id and whether it is passcode protected
WHEN I create a passcode-protected event THEN THE SYSTEM SHALL return the generated passcode
WHEN I create an unprotected event THEN THE SYSTEM SHALL return no passcode
WHEN I omit the organizer name THEN THE SYSTEM SHALL reject the request
WHEN I supply a malformed guest id THEN THE SYSTEM SHALL reject the request

### US-002: Join an event
**As a** participant
**I want to** join an event by providing my name and, when required, the passcode
**So that** I can later mark my availability

**Acceptance Criteria:**
WHEN I join an open event THEN THE SYSTEM SHALL create a participant and return my participant id and name
WHEN I join a passcode-protected event with the correct passcode THEN THE SYSTEM SHALL create a participant
WHEN I supply a wrong or missing passcode for a protected event THEN THE SYSTEM SHALL respond unauthorized
WHEN I join an event I have already joined THEN THE SYSTEM SHALL respond conflict
WHEN I omit my participant name THEN THE SYSTEM SHALL reject the request
WHEN I join an event THEN THE SYSTEM SHALL require my IANA timezone and record it on my participation, even when the event's timezone option is off
WHEN I supply an unknown IANA timezone THEN THE SYSTEM SHALL reject the request
WHEN the event does not exist THEN THE SYSTEM SHALL respond not found

### US-003: Leave an event
**As a** participant
**I want to** leave an event I joined
**So that** I am no longer counted as a participant

**Acceptance Criteria:**
WHEN I leave an event I joined THEN THE SYSTEM SHALL remove me from the event
WHEN I leave an event I did not join THEN THE SYSTEM SHALL do nothing and report success
WHEN the organizer tries to leave their own event THEN THE SYSTEM SHALL respond conflict
WHEN the event does not exist THEN THE SYSTEM SHALL respond not found

### US-004: Retrieve an event
**As an** organizer or participant
**I want to** look up an event by its shareable public id
**So that** I can see its name and whether a passcode is required

**Acceptance Criteria:**
WHEN I request an existing public id THEN THE SYSTEM SHALL return the event's public id, name, passcode-protected flag, and timezone (null when the timezone option is off)
WHEN the public id does not exist THEN THE SYSTEM SHALL respond not found

### US-005: List my events
**As a** guest
**I want to** list every event I have created or joined
**So that** I can find events I care about

**Acceptance Criteria:**
WHEN I search by my guest id THEN THE SYSTEM SHALL return every event I am a participant in, each flagged with whether I am the organizer
WHEN I have no events THEN THE SYSTEM SHALL return an empty list
WHEN I omit or malform my guest id THEN THE SYSTEM SHALL reject the request

### US-006: Toggle timezone-aware display for an event
**As an** organizer
**I want to** turn timezone-aware display on or off for my event, and set its fallback timezone
**So that** participants see availability in their own local time when I need it

**Acceptance Criteria:**
WHEN I create an event THEN THE SYSTEM SHALL leave the timezone option off by default
WHEN I enable or change the timezone option THEN THE SYSTEM SHALL record the IANA timezone and render availability in viewers' timezones
WHEN I disable the timezone option THEN THE SYSTEM SHALL fall back to legacy (no-conversion) behavior
WHEN a non-organizer tries to change the option THEN THE SYSTEM SHALL respond forbidden
WHEN I supply an unknown timezone THEN THE SYSTEM SHALL reject the request

## Functional Requirements

### FR-001: Generate a shareable public id
**Priority:** P0
**Persona:** organizer

WHEN an event is created THEN THE SYSTEM SHALL generate a unique 12-character public id and return it for sharing

### FR-002: Make the organizer a participant
**Priority:** P0
**Persona:** organizer

WHEN an event is created THEN THE SYSTEM SHALL create the organizer as its first participant and record that participant id as the event's organizer

### FR-003: Protect events with a passcode
**Priority:** P1
**Persona:** organizer

WHEN an event is created with protection enabled THEN THE SYSTEM SHALL generate a 6-character passcode, store only its salted hash, and return the plaintext passcode exactly once

### FR-004: Enforce passcode on join
**Priority:** P0
**Persona:** participant

WHEN a participant joins a protected event THEN THE SYSTEM SHALL require the correct passcode and respond unauthorized otherwise

### FR-005: Prevent duplicate joins
**Priority:** P0
**Persona:** participant

WHEN a guest attempts to join an event they are already in THEN THE SYSTEM SHALL respond conflict

### FR-006: Prevent the organizer from leaving
**Priority:** P1
**Persona:** organizer

WHEN the organizer attempts to leave their own event THEN THE SYSTEM SHALL respond conflict

### FR-007: Search events by guest
**Priority:** P0
**Persona:** guest

WHEN a guest searches their events THEN THE SYSTEM SHALL return each event with an organizer flag indicating whether the guest organized it

### FR-008: Optional, toggleable event timezone
**Priority:** P0
**Persona:** organizer

WHEN an event is created THEN THE SYSTEM SHALL leave its timezone option off;
only the organizer SHALL be able to enable, change, or disable it afterwards,
and an enabled option records the event's fallback display timezone.

### FR-009: Capture the participant's timezone on join
**Priority:** P0
**Persona:** participant

WHEN a participant joins an event THEN THE SYSTEM SHALL require an IANA timezone
and record it on the participant, regardless of the event's timezone option, so
the option can be enabled later without re-entry.

## Non-Functional Requirements

### NFR-001: Access control
The only access control is the shareable public id plus the per-browser guest id;
there is no real authentication in this MVP.

### NFR-002: Passcode security
Passcodes are never stored in plaintext; only a PBKDF2-SHA256 hash (100,000
iterations, 16-byte salt, 32-byte output) is persisted, and verification uses a
constant-time comparison.

### NFR-003: Timezone option without backfill
Enabling an event's timezone option later requires no migration or data
re-entry: participant timezones are captured from the moment of joining, and
existing events or participants with no timezone remain valid.

## Out of Scope

- Editing or deleting an event after creation.
- Resetting or rotating a passcode.
- Cross-timezone scheduling: events store an optional timezone and participate
  in timezone-aware rendering, but the system does not suggest a common slot or
  analyze overlaps across timezones.
- Real authentication or accounts beyond the public id + guest id trust model.

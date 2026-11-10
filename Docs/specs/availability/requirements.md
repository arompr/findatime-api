# Availability Requirements

Availability lets participants mark the times they are free on an event's
calendar, and lets organizers view everyone's availability so they can pick a
slot that works for all. It replaces the localStorage-only prototype with
server-backed storage. Availability is exchanged and stored as UTC instants,
aligned by the server to the event's slot grid in the event's timezone. Clients
convert instants to local time only for display.

## User Stories

### US-001: Mark my availability
**As a** participant
**I want to** indicate the time ranges I am free for an event
**So that** the organizer can find a time that works for everyone

**Acceptance Criteria:**
WHEN I submit my availability ranges THEN THE SYSTEM SHALL store them and return my saved availability
WHEN I submit my availability THEN THE SYSTEM SHALL accept the ranges as ISO 8601 instants with an explicit UTC offset
WHEN I submit a range that does not line up with the event's interval (15, 30, or 60 minutes) THEN THE SYSTEM SHALL widen it outward to the nearest grid boundaries in the event's timezone and return the widened instants
WHEN I submit my availability THEN THE SYSTEM SHALL NOT change the timezone recorded when I joined
WHEN the organizer changes the event's interval THEN THE SYSTEM SHALL leave the ranges I already submitted exactly as they were stored, and reads SHALL project them inward onto the new interval grid
WHEN I submit an empty list of ranges THEN THE SYSTEM SHALL clear my previous availability
WHEN I submit a range whose end is not after its start THEN THE SYSTEM SHALL reject the request with an error
WHEN I submit a range longer than 24 hours THEN THE SYSTEM SHALL reject the request with an error
WHEN I submit a bound without a UTC offset THEN THE SYSTEM SHALL reject the request with an error

### US-002: View everyone's availability
**As an** organizer
**I want to** see all participants' availability ranges for an event
**So that** I can pick a slot that works for everyone

**Acceptance Criteria:**
WHEN I request an event's availability THEN THE SYSTEM SHALL return every participant and their ranges
WHEN I request an event's availability THEN THE SYSTEM SHALL project each participant's ranges inward onto the event's current interval grid, anchored in the event's timezone
WHEN a stored range contains no full interval THEN THE SYSTEM SHALL omit it from that participant's ranges rather than widening or shrinking it to a partial slot
WHEN I request an event's availability THEN THE SYSTEM SHALL return each range as UTC instants, identical for every viewer
WHEN the event has no timezone THEN THE SYSTEM SHALL anchor the grid at midnight UTC
WHEN no one has joined the event THEN THE SYSTEM SHALL return an empty participant list

### US-003: View one participant's availability
**As an** organizer
**I want to** read a single participant's availability
**So that** I can deep-link to one person's availability

**Acceptance Criteria:**
WHEN I request a participant's availability for an event they belong to THEN THE SYSTEM SHALL return their ranges
WHEN I request a participant's availability THEN THE SYSTEM SHALL return their ranges as UTC instants
WHEN the participant does not belong to that event THEN THE SYSTEM SHALL respond not found

## Functional Requirements

### FR-001: Resolve the calling participant
**Priority:** P0
**Persona:** participant

WHEN an authenticated guest asks for their participant in an event THEN THE SYSTEM SHALL return the participant id and name for that guest
WHEN the guest has not joined the event THEN THE SYSTEM SHALL respond not found
WHEN the request carries no valid guest token THEN THE SYSTEM SHALL respond unauthorized

### FR-002: Replace a participant's availability
**Priority:** P0
**Persona:** participant

WHEN a participant submits their availability as UTC instants THEN THE SYSTEM SHALL snap each range outward to the event's interval grid in the event's timezone (UTC when it has none) and store the snapped ranges as canonical UTC instants, replacing all their existing ranges
WHEN the requester's guest id does not match the participant THEN THE SYSTEM SHALL respond forbidden

### FR-003: List availability for an event
**Priority:** P0
**Persona:** organizer

WHEN an event's availability is requested THEN THE SYSTEM SHALL return a flat list of participants, each with their ranges projected inward onto the event's current interval grid in the event's timezone, omitting ranges that contain no full interval

### FR-004: Read one participant's availability
**Priority:** P1
**Persona:** organizer

WHEN a specific participant's availability is requested THEN THE SYSTEM SHALL return that participant's ranges, provided they belong to the event

### FR-005: Instant exchange
**Priority:** P0
**Persona:** participant, organizer

WHEN availability is read or written THEN THE SYSTEM SHALL exchange ranges as
ISO 8601 instants with an explicit offset, answer in UTC, and carry no timezone
field; wall-clock conversion for display is the client's job.

### FR-006: Projection onto the current interval
**Priority:** P0
**Persona:** organizer

WHEN availability is read THEN THE SYSTEM SHALL merge each participant's stored
ranges into a union and project that union inward onto the event's current
interval grid, anchored at midnight in the event's timezone (UTC when the
event has no timezone), omitting blocks that contain no full interval.
Projection SHALL NOT modify stored rows, and every viewer SHALL receive the same
projected instants.

## Non-Functional Requirements

### NFR-001: Access control
Access control is the shareable public id plus the server-issued guest token;
only participants can read an event's availability;
there is no real authentication in this MVP.

### NFR-002: Timezone transparency
The backend owns all grid arithmetic, in the event's timezone. Instants cross
the wire unchanged, so a client whose tz database differs from the server's can
mislabel times but never misplaces a range relative to the event or to other
participants.

## Out of Scope

- Cross-timezone scheduling: the system stores and renders times in timezones
  but does not suggest a common slot or analyze overlaps.
- Overlap validation: the server enforces end-after-start and a 24-hour maximum
  duration only; it does not reject overlapping ranges.
- User accounts beyond the guest-token model.

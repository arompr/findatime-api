# Availability Requirements

Availability lets participants mark the times they are free on an event's
calendar, and lets organizers view everyone's availability so they can pick a
slot that works for all. It replaces the localStorage-only prototype with
server-backed storage.

## User Stories

### US-001: Mark my availability
**As a** participant
**I want to** indicate the time ranges I am free for an event
**So that** the organizer can find a time that works for everyone

**Acceptance Criteria:**
WHEN I submit my availability ranges THEN THE SYSTEM SHALL store them and return my saved availability
WHEN I submit an empty list of ranges THEN THE SYSTEM SHALL clear my previous availability
WHEN I submit a range whose end is not after its start THEN THE SYSTEM SHALL reject the request with an error
WHEN I submit a range longer than 24 hours THEN THE SYSTEM SHALL reject the request with an error

### US-002: View everyone's availability
**As an** organizer
**I want to** see all participants' availability ranges for an event
**So that** I can pick a slot that works for everyone

**Acceptance Criteria:**
WHEN I request an event's availability THEN THE SYSTEM SHALL return every participant and their ranges
WHEN no one has joined the event THEN THE SYSTEM SHALL return an empty participant list

### US-003: View one participant's availability
**As an** organizer
**I want to** read a single participant's availability
**So that** I can deep-link to one person's availability

**Acceptance Criteria:**
WHEN I request a participant's availability for an event they belong to THEN THE SYSTEM SHALL return their ranges
WHEN the participant does not belong to that event THEN THE SYSTEM SHALL respond not found

## Functional Requirements

### FR-001: Resolve the calling participant
**Priority:** P0
**Persona:** participant

WHEN a request supplies a valid guest id header for an event THEN THE SYSTEM SHALL return the participant id and name for that guest
WHEN the guest has not joined the event THEN THE SYSTEM SHALL respond not found
WHEN the guest id header is missing or malformed THEN THE SYSTEM SHALL respond bad request

### FR-002: Replace a participant's availability
**Priority:** P0
**Persona:** participant

WHEN a participant submits their availability THEN THE SYSTEM SHALL replace all their existing ranges with the submitted ones
WHEN the requester's guest id does not match the participant THEN THE SYSTEM SHALL respond forbidden

### FR-003: List availability for an event
**Priority:** P0
**Persona:** organizer

WHEN an event's availability is requested THEN THE SYSTEM SHALL return a flat list of participants, each with their ranges

### FR-004: Read one participant's availability
**Priority:** P1
**Persona:** organizer

WHEN a specific participant's availability is requested THEN THE SYSTEM SHALL return that participant's ranges, provided they belong to the event

## Non-Functional Requirements

### NFR-001: Access control
The only access control is the shareable public id plus the per-browser guest id;
there is no real authentication in this MVP.

## Out of Scope

- Timezone: events and participants carry a timezone column but it is always
  null; timezone-aware scheduling is a future feature.
- Overlap validation: the server enforces end-after-start and a 24-hour maximum
  duration only; it does not reject overlapping ranges.
- Real authentication beyond the public id + guest id trust model.

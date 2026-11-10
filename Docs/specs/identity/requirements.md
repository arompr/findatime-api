# Identity Requirements

Identity lets Findatime recognize the same person across requests without
asking them to sign up. Each browser holds a server-issued guest token; the
API resolves it to a guest, and that guest is who organizes, joins and answers
events.

## User Stories

### US-001: Be recognized without an account
**As a** guest
**I want to** be recognized on my device without creating an account
**So that** I keep my events and my organizer rights with no signup

**Acceptance Criteria:**
WHEN I first use the app THEN THE SYSTEM SHALL issue me a guest token
WHEN I call the API with my token THEN THE SYSTEM SHALL act as my guest
WHEN I call the API without a valid token THEN THE SYSTEM SHALL respond unauthorized

### US-002: Keep my events after the switch to tokens
**As a** guest who used Findatime before guest tokens existed
**I want to** trade my old browser id for a token once
**So that** I keep the events I created or joined

**Acceptance Criteria:**
WHEN I present my old id for the first time THEN THE SYSTEM SHALL give me a token for that same identity
WHEN someone presents an old id that was already claimed THEN THE SYSTEM SHALL respond conflict

## Functional Requirements

### FR-001: Issue guest tokens
**Priority:** P0
**Persona:** guest

WHEN a client asks for a guest THEN THE SYSTEM SHALL return an opaque random token and never the guest id

**Rationale:** the identity must be chosen by the server and never travel in URLs.

### FR-002: Adopt legacy guest ids
**Priority:** P0
**Persona:** guest

WHEN a client presents an unclaimed legacy guest id that has participations THEN THE SYSTEM SHALL issue a token bound to that id

**Rationale:** existing users keep their events; temporary, behind `Identity:AllowLegacyAdoption`.

### FR-003: Rate limiting
**Priority:** P1
**Persona:** operator

WHEN a client IP exceeds the per-minute limit for guest creation or joining THEN THE SYSTEM SHALL respond too many requests

**Rationale:** prevents token farming and passcode brute force.

## Non-Functional Requirements

### NFR-001: Secret storage
Only a SHA-256 hash of each token is stored; a database leak does not reveal usable tokens.

## Out of Scope

- User accounts, social login and cross-device sync (planned: accounts will claim guest ids).
- Token rotation, revocation UI and expiry.

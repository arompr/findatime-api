# Initial Availability: Requirements

## Goal

Replace the localStorage-only availability prototype with server-backed
availability: participants persist their free-time ranges, and organizers read
everyone's ranges.

## Scope

In scope:
- Resolve the calling participant (`GET /events/{publicId}/participant`).
- List all participants' availability (`GET /events/{publicId}/availabilities`).
- Read one participant's availability (`GET /events/{publicId}/participants/{participantId}/availability`).
- Replace one participant's availability (`PUT /events/{publicId}/participants/{participantId}/availability`).

Out of scope:
- Timezone-aware scheduling (columns ship, but remain null).
- Real authentication (public id + guest id trust model only).
- Overlap validation.

## Acceptance Criteria

- [ ] A participant can resolve themselves into a participant record for an event.
- [ ] A participant can replace their availability; it persists and is visible to reads.
- [ ] An organizer can read all participants' availability in a single list.
- [ ] An organizer can read one participant's availability.
- [ ] Ownership is enforced: a guest can only replace their own availability.

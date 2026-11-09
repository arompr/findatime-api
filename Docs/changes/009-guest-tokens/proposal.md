# 009-guest-tokens: Proposal

## Goal

Replace the client-chosen guest UUID with a server-issued, opaque guest token,
so that identity can no longer be forged or leaked through URLs, and enforce
that only participants can read an event's availability.

## Scope

In scope:
- `POST /guests` issues a guest token (stored hashed); optional one-time
  adoption of a legacy browser guest id so existing users keep their events.
- `Authorization: Guest <token>` authentication on every endpoint that needs a
  caller; `X-Guest-Id`, body `guestId` and query `guestId` are removed.
- Availability reads require the caller to be a participant of the event.
- Per-IP rate limiting on guest creation and on joining (passcode brute force).
- OpenAPI security scheme; Hurl files, specs and docs updated.

Out of scope:
- User accounts / social login / cross-device sync (next phase; accounts will
  claim guest identities).
- UI changes (separate change in `findatime-ui`).
- Removing the legacy adoption path (later change, once clients migrated).

## Rationale

The guest id is a bearer credential that the client invents and the server
trusts, and it travels in query strings. A server-issued token, persisted only
as a hash, fixes forgery and leakage with no UX change and keeps
`participants.guest_id` as the identity key, so accounts can later link guest
ids without a domain rewrite.

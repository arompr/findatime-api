# Findatime - Availability REST scratch file.
# Run: hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl
#
# Inspect a single request: hurl --to-entry N <file> runs entries 1..N, reuses
# the captures made by the earlier entries, and prints only the last response
# body. (--from-entry N alone cannot be used for id-dependent entries: their ids
# come from captures in the entries before them.)
#
# Entries:
#   1  POST   /events
#   2  POST   /events/{publicId}/join
#   3  GET    /events/{publicId}/participant
#   4  QUERY  /events/{publicId}/availabilities
#   5  QUERY  /events/{publicId}/participants/{participantId}/availability
#   6  PUT    /events/{publicId}/participants/{participantId}/availability
#
# Static variables are defined in the first entry's [Options] and inherited by
# the entries that follow. The first two entries set up the event and participant
# the later calls operate on: the event's publicId and the participant's id are
# captured from responses. Availability reads use the non-standard QUERY verb.

POST {{baseUrl}}/events
[Options]
variable: baseUrl="http://localhost:5263"
variable: organizerGuestId="4a7b3c2d-1e2f-3a4b-5c6d-7e8f9a0b1c2d"
variable: participantGuestId="9c8d7e6f-5a4b-3c2d-1e0f-9a8b7c6d5e4f"
{
    "name": "Saturday Night Local",
    "guestId": "{{organizerGuestId}}",
    "organizerName": "Alice",
    "isPasscodeProtected": false
}

HTTP 201
[Captures]
publicId: jsonpath "$.publicId"

# Join as the participant whose availability is read and written below
POST {{baseUrl}}/events/{{publicId}}/join
{
    "guestId": "{{participantGuestId}}",
    "participantName": "Bob",
    "timezone": "America/New_York"
}

HTTP 201
[Captures]
participantId: jsonpath "$.participantId"

# Resolve the calling participant
GET {{baseUrl}}/events/{{publicId}}/participant
X-Guest-Id: {{participantGuestId}}

HTTP 200

# List event availability (viewer frame; omit timezone -> event frame -> UTC)
QUERY {{baseUrl}}/events/{{publicId}}/availabilities
{
  "timezone": "Europe/Berlin"
}

HTTP 200

# Read one participant's availability
QUERY {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
{
  "timezone": "America/New_York"
}

HTTP 200

# Replace one participant's availability (wall-clock, no UTC offset)
PUT {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
X-Guest-Id: {{participantGuestId}}
{
  "timezone": "Europe/Berlin",
  "ranges": [
    { "start": "2026-09-21T09:00:00", "end": "2026-09-21T10:00:00" },
    { "start": "2026-09-21T14:00:00", "end": "2026-09-21T15:00:00" }
  ]
}

HTTP 200

# Findatime - Availability REST scratch file.
# Run: hurl --test src/FindAtime.Api/Features/Availability/Api/availability.hurl
#
# Inspect a single request: hurl --to-entry N <file> runs entries 1..N, reuses
# the captures made by the earlier entries, and prints only the last response
# body. (--from-entry N alone cannot be used for id-dependent entries: their ids
# come from captures in the entries before them.)
#
# Entries:
#   1  POST   /guests (organizer token)
#   2  POST   /guests (participant token)
#   3  POST   /guests (outsider token: never joins)
#   4  POST   /events
#   5  PUT    /events/{publicId}/params (organizer anchors the event to Europe/Berlin)
#   6  POST   /events/{publicId}/join
#   7  GET    /events/{publicId}/participant
#   8  GET    /events/{publicId}/availabilities
#   9  GET    /events/{publicId}/availabilities (outsider -> 403)
#  10  GET    /events/{publicId}/participants/{participantId}/availability
#  11  GET    /events/{publicId}/participants/{participantId}/availability (outsider -> 403)
#  12  PUT    /events/{publicId}/participants/{participantId}/availability (snaps outward on the event's 30-min grid)
#  13  PUT    /events/{publicId}/participants/{participantId}/availability (range without a UTC offset -> 400)
#  14  PUT    /events/{publicId}/participants/{participantId}/availability (organizer writing someone else's -> 403)
#  15  PUT    /events/{publicId}/params (organizer switches the interval to 60)
#  16  GET    /events/{publicId}/participants/{participantId}/availability (projects onto the 60-min grid)
#  17  GET    /events/{publicId}/availabilities (projects onto the 60-min grid)
#
# Every call authenticates with `Authorization: Guest <token>`; tokens come from
# POST /guests. Availability reads are limited to the event's participants. The
# event's publicId and the participant's id are captured from responses.
# Availability is exchanged as UTC instants; the server snaps writes and
# projects reads on the event's grid in the event timezone.

POST {{baseUrl}}/guests
[Options]
variable: baseUrl="http://localhost:5263"

HTTP 201
[Captures]
organizerToken: jsonpath "$.guestToken"

POST {{baseUrl}}/guests

HTTP 201
[Captures]
participantToken: jsonpath "$.guestToken"

POST {{baseUrl}}/guests

HTTP 201
[Captures]
outsiderToken: jsonpath "$.guestToken"

POST {{baseUrl}}/events
Authorization: Guest {{organizerToken}}
{
    "name": "Saturday Night Local",
    "organizerName": "Alice",
    "isPasscodeProtected": false
}

HTTP 201
[Captures]
publicId: jsonpath "$.publicId"

# Anchor the event to Europe/Berlin (UTC+2 in September) on the default 30-min grid
PUT {{baseUrl}}/events/{{publicId}}/params
Authorization: Guest {{organizerToken}}
{
  "timezone": "Europe/Berlin",
  "intervalMinutes": 30,
  "dayStartHour": 9,
  "dayEndHour": 21
}

HTTP 200

# Join as the participant whose availability is read and written below
POST {{baseUrl}}/events/{{publicId}}/join
Authorization: Guest {{participantToken}}
{
    "participantName": "Bob",
    "timezone": "America/New_York"
}

HTTP 201
[Captures]
participantId: jsonpath "$.participantId"

# Resolve the calling participant
GET {{baseUrl}}/events/{{publicId}}/participant
Authorization: Guest {{participantToken}}

HTTP 200

# List event availability (UTC instants)
GET {{baseUrl}}/events/{{publicId}}/availabilities
Authorization: Guest {{participantToken}}

HTTP 200
[Asserts]
jsonpath "$.timezone" not exists

# A guest who has not joined cannot read the event's availability
GET {{baseUrl}}/events/{{publicId}}/availabilities
Authorization: Guest {{outsiderToken}}

HTTP 403
[Asserts]
jsonpath "$.error" == "forbidden"

# Read one participant's availability
GET {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
Authorization: Guest {{participantToken}}

HTTP 200

# ...not as an outsider
GET {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
Authorization: Guest {{outsiderToken}}

HTTP 403

# Replace one participant's availability (ISO 8601 instants with an offset; the
# misaligned 16:05-16:50Z (18:05-18:50 Berlin) range is widened on the Berlin
# grid to 16:00-17:00Z). The +02:00 range is normalized to UTC. 06:30-08:00Z
# and 09:30-10:30Z are aligned to the default 30-min grid; the next entries
# switch the event to 60 minutes and prove how they are projected.
PUT {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
Authorization: Guest {{participantToken}}
{
  "ranges": [
    { "start": "2026-09-21T06:30:00Z", "end": "2026-09-21T08:00:00Z" },
    { "start": "2026-09-21T11:30:00+02:00", "end": "2026-09-21T12:30:00+02:00" },
    { "start": "2026-09-21T16:05:00Z", "end": "2026-09-21T16:50:00Z" }
  ]
}

HTTP 200
[Asserts]
jsonpath "$.timezone" not exists
jsonpath "$.ranges[0].start" == "2026-09-21T06:30:00+00:00"
jsonpath "$.ranges[0].end" == "2026-09-21T08:00:00+00:00"
jsonpath "$.ranges[1].start" == "2026-09-21T09:30:00+00:00"
jsonpath "$.ranges[1].end" == "2026-09-21T10:30:00+00:00"
jsonpath "$.ranges[2].start" == "2026-09-21T16:00:00+00:00"
jsonpath "$.ranges[2].end" == "2026-09-21T17:00:00+00:00"

# A range without a UTC offset is rejected
PUT {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
Authorization: Guest {{participantToken}}
{
  "ranges": [
    { "start": "2026-09-21T08:30:00", "end": "2026-09-21T10:00:00" }
  ]
}

HTTP 400
[Asserts]
jsonpath "$.error" == "invalid_availability_range"

# The organizer cannot overwrite another participant's availability
PUT {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
Authorization: Guest {{organizerToken}}
{
  "ranges": []
}

HTTP 403
[Asserts]
jsonpath "$.error" == "forbidden"

# The organizer switches the event to 1-hour slots
PUT {{baseUrl}}/events/{{publicId}}/params
Authorization: Guest {{organizerToken}}
{
  "timezone": "Europe/Berlin",
  "intervalMinutes": 60,
  "dayStartHour": 9,
  "dayEndHour": 21
}

HTTP 200
[Asserts]
jsonpath "$.intervalMinutes" == 60

# Read one participant's availability on the 60-min Berlin grid: 06:30-08:00Z
# (08:30-10:00 Berlin) becomes 07:00-08:00Z, 09:30-10:30Z covers no full hour and
# disappears, 16:00-17:00Z is already aligned
GET {{baseUrl}}/events/{{publicId}}/participants/{{participantId}}/availability
Authorization: Guest {{participantToken}}

HTTP 200
[Asserts]
jsonpath "$.ranges" count == 2
jsonpath "$.ranges[0].start" == "2026-09-21T07:00:00+00:00"
jsonpath "$.ranges[0].end" == "2026-09-21T08:00:00+00:00"
jsonpath "$.ranges[1].start" == "2026-09-21T16:00:00+00:00"
jsonpath "$.ranges[1].end" == "2026-09-21T17:00:00+00:00"

# The event-wide read applies the same projection (participants ordered by name:
# Alice, then Bob)
GET {{baseUrl}}/events/{{publicId}}/availabilities
Authorization: Guest {{organizerToken}}

HTTP 200
[Asserts]
jsonpath "$.participants[1].name" == "Bob"
jsonpath "$.participants[1].ranges" count == 2
jsonpath "$.participants[1].ranges[0].start" == "2026-09-21T07:00:00+00:00"
jsonpath "$.participants[1].ranges[1].start" == "2026-09-21T16:00:00+00:00"
jsonpath "$.participants[0].ranges" count == 0

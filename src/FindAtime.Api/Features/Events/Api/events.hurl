# Findatime - Events REST scratch file.
# Run: hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl
#
# Inspect a single request: hurl --to-entry N <file> runs entries 1..N, reuses
# the captures made by the earlier entries, and prints only the last response
# body. (--from-entry N alone cannot be used for id-dependent entries: their ids
# come from captures in the entries before them.)
#
# Entries:
#   1  POST   /events
#   2  GET    /events?guestId=
#   3  GET    /events/{publicId}
#   4  PUT    /events/{publicId}/params      (X-Guest-Id)
#   5  POST   /events/{publicId}/join
#   6  POST   /events/{publicId}/leave
#
# Static variables are defined in the first entry's [Options] and inherited by
# the entries that follow. Ids created by earlier calls (the event's publicId and
# the participant's id) are captured from responses and reused below.

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

# Search events by guest
GET {{baseUrl}}/events?guestId={{organizerGuestId}}

HTTP 200

# Get event by public id
GET {{baseUrl}}/events/{{publicId}}

HTTP 200

# Update event params (organizer only; null timezone disables the option)
PUT {{baseUrl}}/events/{{publicId}}/params
X-Guest-Id: {{organizerGuestId}}
{
    "timezone": "Europe/Berlin"
}

HTTP 200

# Join event
POST {{baseUrl}}/events/{{publicId}}/join
{
    "passcode": "ABC234",
    "guestId": "{{participantGuestId}}",
    "participantName": "Bob",
    "timezone": "America/New_York"
}

HTTP 201
[Captures]
participantId: jsonpath "$.participantId"

# Leave event
POST {{baseUrl}}/events/{{publicId}}/leave
{
    "guestId": "{{participantGuestId}}"
}

HTTP 204

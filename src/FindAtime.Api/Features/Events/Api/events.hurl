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
#   4  PUT    /events/{publicId}/params      (X-Guest-Id; timezone, intervalMinutes, dayStartHour, dayEndHour required)
#   5  PUT    /events/{publicId}/params      (inverted day window -> 400 invalid_day_window)
#   6  POST   /events/{publicId}/join
#   7  POST   /events/{publicId}/leave
#   8  DELETE /events/{publicId}             (missing X-Guest-Id -> 400)
#   9  DELETE /events/{publicId}             (blank X-Guest-Id -> 400)
#  10  DELETE /events/{publicId}             (malformed X-Guest-Id -> 400)
#  11  DELETE /events/{publicId}             (X-Guest-Id; organizer only -> 204)
#  12  GET    /events/{publicId}             (assert 404 after delete)
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
    "isPasscodeProtected": false,
    "timezone": "Europe/Paris"
}

HTTP 201
[Captures]
publicId: jsonpath "$.publicId"

# Search events by guest
GET {{baseUrl}}/events?guestId={{organizerGuestId}}

HTTP 200

# Get event by public id (new events default to a 09:00-21:00 day window, anchored to the creator timezone)
GET {{baseUrl}}/events/{{publicId}}

HTTP 200
[Asserts]
jsonpath "$.dayStartHour" == 9
jsonpath "$.dayEndHour" == 21
jsonpath "$.timezone" == "Europe/Paris"

# Update event params (organizer only; full replace, all four fields required)
PUT {{baseUrl}}/events/{{publicId}}/params
X-Guest-Id: {{organizerGuestId}}
{
    "timezone": "Europe/Berlin",
    "intervalMinutes": 15,
    "dayStartHour": 9,
    "dayEndHour": 17
}

HTTP 200
[Asserts]
jsonpath "$.intervalMinutes" == 15
jsonpath "$.timezone" == "Europe/Berlin"
jsonpath "$.dayStartHour" == 9
jsonpath "$.dayEndHour" == 17

# Update event params with an inverted day window (400 invalid_day_window)
PUT {{baseUrl}}/events/{{publicId}}/params
X-Guest-Id: {{organizerGuestId}}
{
    "timezone": "Europe/Berlin",
    "intervalMinutes": 15,
    "dayStartHour": 17,
    "dayEndHour": 9
}

HTTP 400
[Asserts]
jsonpath "$.error" == "invalid_day_window"

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

# Delete with a missing X-Guest-Id header (request-shape 400)
DELETE {{baseUrl}}/events/{{publicId}}

HTTP 400

# Delete with a blank X-Guest-Id header (request-shape 400)
DELETE {{baseUrl}}/events/{{publicId}}
X-Guest-Id:

HTTP 400

# Delete with a malformed X-Guest-Id header (request-shape 400)
DELETE {{baseUrl}}/events/{{publicId}}
X-Guest-Id: not-a-uuid

HTTP 400

# Delete the event (organizer only)
DELETE {{baseUrl}}/events/{{publicId}}
X-Guest-Id: {{organizerGuestId}}

HTTP 204

# Assert the event is gone
GET {{baseUrl}}/events/{{publicId}}

HTTP 404

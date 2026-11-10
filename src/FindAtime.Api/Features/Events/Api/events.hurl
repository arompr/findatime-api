# Findatime - Events REST scratch file.
# Run: hurl --test src/FindAtime.Api/Features/Events/Api/events.hurl
#
# Inspect a single request: hurl --to-entry N <file> runs entries 1..N, reuses
# the captures made by the earlier entries, and prints only the last response
# body. (--from-entry N alone cannot be used for id-dependent entries: their ids
# come from captures in the entries before them.)
#
# Entries:
#   1  POST   /guests                        (organizer token)
#   2  POST   /guests                        (participant token)
#   3  POST   /events
#   4  GET    /events                        (the caller's events)
#   5  GET    /events/{publicId}             (public, no token)
#   6  PUT    /events/{publicId}/params      (organizer; timezone, intervalMinutes, dayStartHour, dayEndHour required)
#   7  PUT    /events/{publicId}/params      (inverted day window -> 400 invalid_day_window)
#   8  POST   /events/{publicId}/join
#   9  PUT    /events/{publicId}/params      (participant, not organizer -> 403)
#  10  DELETE /events/{publicId}             (participant, not organizer -> 403)
#  11  POST   /events/{publicId}/leave
#  12  DELETE /events/{publicId}             (no token -> 401)
#  13  DELETE /events/{publicId}             (organizer only -> 204)
#  14  GET    /events/{publicId}             (assert 404 after delete)
#
# Every call except GET /events/{publicId} authenticates with
# `Authorization: Guest <token>`; tokens come from POST /guests and are captured
# below. Ids created by earlier calls (the event's publicId and the
# participant's id) are captured from responses and reused below.

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

POST {{baseUrl}}/events
Authorization: Guest {{organizerToken}}
{
    "name": "Saturday Night Local",
    "organizerName": "Alice",
    "isPasscodeProtected": false,
    "timezone": "Europe/Paris"
}

HTTP 201
[Captures]
publicId: jsonpath "$.publicId"

# The caller's events (organizer of one)
GET {{baseUrl}}/events
Authorization: Guest {{organizerToken}}

HTTP 200
[Asserts]
jsonpath "$.events" count == 1
jsonpath "$.events[0].isOrganizer" == true

# Get event by public id (new events default to a 09:00-21:00 day window, anchored to the creator timezone)
GET {{baseUrl}}/events/{{publicId}}

HTTP 200
[Asserts]
jsonpath "$.dayStartHour" == 9
jsonpath "$.dayEndHour" == 21
jsonpath "$.timezone" == "Europe/Paris"

# Update event params (organizer only; full replace, all four fields required)
PUT {{baseUrl}}/events/{{publicId}}/params
Authorization: Guest {{organizerToken}}
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
Authorization: Guest {{organizerToken}}
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
Authorization: Guest {{participantToken}}
{
    "passcode": "ABC234",
    "participantName": "Bob",
    "timezone": "America/New_York"
}

HTTP 201
[Captures]
participantId: jsonpath "$.participantId"

# A participant cannot change the event's params
PUT {{baseUrl}}/events/{{publicId}}/params
Authorization: Guest {{participantToken}}
{
    "timezone": "Europe/Berlin",
    "intervalMinutes": 30,
    "dayStartHour": 9,
    "dayEndHour": 17
}

HTTP 403
[Asserts]
jsonpath "$.error" == "forbidden"

# A participant cannot delete the event
DELETE {{baseUrl}}/events/{{publicId}}
Authorization: Guest {{participantToken}}

HTTP 403
[Asserts]
jsonpath "$.error" == "forbidden"

# Leave event (no body; the caller is the token's guest)
POST {{baseUrl}}/events/{{publicId}}/leave
Authorization: Guest {{participantToken}}

HTTP 204

# Delete without a token
DELETE {{baseUrl}}/events/{{publicId}}

HTTP 401
[Asserts]
jsonpath "$.error" == "unauthorized"

# Delete the event (organizer only)
DELETE {{baseUrl}}/events/{{publicId}}
Authorization: Guest {{organizerToken}}

HTTP 204

# Assert the event is gone
GET {{baseUrl}}/events/{{publicId}}

HTTP 404

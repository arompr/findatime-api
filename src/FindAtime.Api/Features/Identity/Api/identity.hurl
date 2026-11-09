# Findatime - Identity REST scratch file.
# Run: hurl --test src/FindAtime.Api/Features/Identity/Api/identity.hurl
#
# Inspect a single request: hurl --to-entry N <file> runs entries 1..N, reuses
# the captures made by the earlier entries, and prints only the last response
# body.
#
# Entries:
#   1  POST   /guests                       (new guest -> 201 { guestToken })
#   2  GET    /events                       (Authorization: Guest <token> -> 200, empty)
#   3  GET    /events                       (no Authorization -> 401 unauthorized)
#   4  GET    /events                       (unknown token -> 401 unauthorized)
#   5  POST   /guests                       (legacyGuestId with no participations -> fresh guest)
#   6  POST   /guests                       (malformed legacyGuestId -> 400)
#
# Legacy adoption of an id that has participations (and the 409 on a second
# adoption) needs a pre-existing legacy participant, which the API can no longer
# create; it is covered by AdoptLegacyGuestTests.

POST {{baseUrl}}/guests
[Options]
variable: baseUrl="http://localhost:5263"

HTTP 201
[Captures]
guestToken: jsonpath "$.guestToken"
[Asserts]
jsonpath "$.guestToken" isString
jsonpath "$.guestId" not exists

# The new guest has no events yet
GET {{baseUrl}}/events
Authorization: Guest {{guestToken}}

HTTP 200
[Asserts]
jsonpath "$.events" count == 0

# Missing token
GET {{baseUrl}}/events

HTTP 401
[Asserts]
jsonpath "$.error" == "unauthorized"

# Unknown token
GET {{baseUrl}}/events
Authorization: Guest not-a-real-token

HTTP 401
[Asserts]
jsonpath "$.error" == "unauthorized"

# A legacy id that never joined anything just yields a fresh guest
POST {{baseUrl}}/guests
{
    "legacyGuestId": "{{newUuid}}"
}

HTTP 201
[Asserts]
jsonpath "$.guestToken" isString

# Malformed legacy id
POST {{baseUrl}}/guests
{
    "legacyGuestId": "not-a-uuid"
}

HTTP 400

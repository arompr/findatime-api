SELECT e.public_id, e.name, (e.organizer_participant_id = p.id) AS is_organizer
FROM events e
JOIN participants p ON p.event_id = e.id
WHERE p.guest_id = @guestId

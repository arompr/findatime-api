SELECT p.id AS participant_id, p.name
FROM participants p
JOIN events e ON e.id = p.event_id
WHERE e.public_id = @publicId AND p.guest_id = @guestId

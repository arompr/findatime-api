SELECT timezone
FROM events
WHERE public_id = @publicId;

SELECT p.id AS participant_id, p.name, a.start_utc, a.end_utc
FROM participants p
LEFT JOIN availabilities a ON a.participant_id = p.id
WHERE p.event_id = (SELECT id FROM events WHERE public_id = @publicId)
ORDER BY p.name, p.id, a.start_utc

SELECT timezone, interval_minutes
FROM events
WHERE public_id = @publicId;

SELECT p.id AS participant_id, p.name, a.start_utc, a.end_utc
FROM participants p
INNER JOIN events e
    ON e.id = p.event_id
LEFT JOIN availabilities a
    ON a.participant_id = p.id
WHERE e.public_id = @publicId
ORDER BY p.name, p.id, a.start_utc

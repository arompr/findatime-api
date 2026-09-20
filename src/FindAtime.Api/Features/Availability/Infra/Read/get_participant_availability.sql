SELECT p.id AS participant_id, p.name, a.start_utc, a.end_utc
FROM participants p
INNER JOIN events e
    ON e.id = p.event_id
LEFT JOIN availabilities a
    ON a.participant_id = p.id
WHERE p.id = @participantId
    AND e.public_id = @publicId
ORDER BY a.start_utc

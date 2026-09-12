SELECT public_id, name
FROM events e
WHERE EXISTS (
    SELECT 1
    FROM participants p
    WHERE p.event_id = e.id
      AND p.guest_id = @guestId
)

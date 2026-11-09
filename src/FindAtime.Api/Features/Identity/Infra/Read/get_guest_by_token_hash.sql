SELECT id AS guest_id, last_seen_at
FROM guests
WHERE token_hash = @tokenHash

SELECT public_id, name, timezone, interval_minutes, (passcode_hash IS NOT NULL) AS is_passcode_protected
FROM events
WHERE public_id = @publicId

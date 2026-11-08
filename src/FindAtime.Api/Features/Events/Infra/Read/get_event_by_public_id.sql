SELECT public_id, name, timezone, interval_minutes, day_start_hour, day_end_hour, (passcode_hash IS NOT NULL) AS is_passcode_protected
FROM events
WHERE public_id = @publicId

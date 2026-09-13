SELECT public_id, name, (passcode_hash IS NOT NULL) AS is_passcode_protected
FROM events
WHERE public_id = @publicId

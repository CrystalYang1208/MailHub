-- Persist cooldown before a MailAccount exists. Fingerprints contain no plaintext address.
CREATE TABLE IF NOT EXISTS AccountValidationFailure (
 fingerprint BLOB PRIMARY KEY CHECK(length(fingerprint)=32),
 failures INTEGER NOT NULL CHECK(failures>=0),
 locked_until_ms INTEGER
) STRICT;

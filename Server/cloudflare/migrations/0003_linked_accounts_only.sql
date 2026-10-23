-- Accountless, ten-minute OAuth handoff. No guest account or cloud save is created.
-- Keep the old migration unchanged for installations that already applied it.
CREATE TABLE IF NOT EXISTS cloud_handoff (
  ticket_hash TEXT PRIMARY KEY,
  poll_hash TEXT NOT NULL,
  provider TEXT NOT NULL,
  mode TEXT NOT NULL,
  expires INTEGER NOT NULL,
  firebase_uid TEXT,
  claimed INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS cloud_handoff_expires ON cloud_handoff(expires);

-- Apply once, after 0003, before deploying the shared account portal.
-- Existing game accounts and saves are preserved.
ALTER TABLE cloud_handoff ADD COLUMN profile_json TEXT;
CREATE TABLE IF NOT EXISTS cloud_profile (
  account_id TEXT PRIMARY KEY REFERENCES cloud_account(id) ON DELETE CASCADE,
  display_name TEXT NOT NULL DEFAULT '',
  email TEXT,
  providers TEXT NOT NULL DEFAULT '[]',
  updated INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS cloud_web_session (
  token_hash TEXT PRIMARY KEY,
  account_id TEXT NOT NULL REFERENCES cloud_account(id) ON DELETE CASCADE,
  expires INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS cloud_web_session_account ON cloud_web_session(account_id);
CREATE INDEX IF NOT EXISTS cloud_web_session_expires ON cloud_web_session(expires);
-- Prevent a partly completed deletion from recreating a profile on retry/login.
-- Kept for 24 hours after successful Firebase deletion to block in-flight login
-- races. Pending failures remain retryable. Contains no email or progress.
CREATE TABLE IF NOT EXISTS cloud_deletion (
  firebase_uid TEXT PRIMARY KEY,
  requested INTEGER NOT NULL,
  completed INTEGER
);

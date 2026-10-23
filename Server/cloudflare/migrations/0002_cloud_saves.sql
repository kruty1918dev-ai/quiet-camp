-- Separate from complimentary Pro. No purchase or consent data in this schema.
CREATE TABLE IF NOT EXISTS cloud_account (
  id TEXT PRIMARY KEY,
  firebase_uid TEXT UNIQUE,
  provider TEXT,
  created INTEGER NOT NULL,
  touched INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS cloud_account_anonymous_touched ON cloud_account(touched) WHERE firebase_uid IS NULL;
CREATE TABLE IF NOT EXISTS cloud_session (
  token_hash TEXT PRIMARY KEY,
  account_id TEXT NOT NULL REFERENCES cloud_account(id) ON DELETE CASCADE,
  checked INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS cloud_session_account ON cloud_session(account_id);
CREATE INDEX IF NOT EXISTS cloud_session_checked ON cloud_session(checked);
CREATE TABLE IF NOT EXISTS cloud_save (
  account_id TEXT PRIMARY KEY REFERENCES cloud_account(id) ON DELETE CASCADE,
  revision INTEGER NOT NULL,
  payload TEXT NOT NULL,
  updated INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS cloud_login (
  ticket_hash TEXT PRIMARY KEY,
  poll_hash TEXT NOT NULL,
  account_id TEXT NOT NULL REFERENCES cloud_account(id) ON DELETE CASCADE,
  provider TEXT NOT NULL,
  mode TEXT NOT NULL,
  expires INTEGER NOT NULL,
  firebase_uid TEXT,
  claimed INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS cloud_login_expires ON cloud_login(expires);

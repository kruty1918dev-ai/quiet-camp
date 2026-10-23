CREATE TABLE complimentary_session (token_hash TEXT PRIMARY KEY CHECK(length(token_hash)=64), generation TEXT NOT NULL CHECK(length(generation)=64), checked INTEGER NOT NULL);
CREATE INDEX complimentary_session_checked ON complimentary_session(checked);
CREATE TABLE complimentary_limit (key TEXT PRIMARY KEY, count INTEGER NOT NULL CHECK(count>0), expires INTEGER NOT NULL);
CREATE INDEX complimentary_limit_expires ON complimentary_limit(expires);

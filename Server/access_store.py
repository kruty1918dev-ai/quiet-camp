"""Durable complimentary sessions and shared rate limits. Never store raw tokens or IPs."""
import time


class SqliteAccessStore:
    def __init__(self, store, clock=time.time):
        self.store, self.clock = store, clock
        with store.connect() as db:
            db.execute('CREATE TABLE IF NOT EXISTS complimentary_session (token_hash TEXT PRIMARY KEY, generation TEXT NOT NULL, checked INTEGER NOT NULL)')
            db.execute('CREATE TABLE IF NOT EXISTS complimentary_limit (key TEXT PRIMARY KEY, count INTEGER NOT NULL, expires INTEGER NOT NULL)')

    def allow(self, key, maximum, seconds):
        now = int(self.clock())
        with self.store.connect() as db:
            # Serializes the read/increment across workers, including after a restart.
            db.execute('BEGIN IMMEDIATE')
            db.execute('DELETE FROM complimentary_limit WHERE expires <= ?', (now,))
            db.execute('INSERT INTO complimentary_limit VALUES (?,1,?) ON CONFLICT(key) DO UPDATE SET count=count+1', (key, now + seconds))
            return db.execute('SELECT count FROM complimentary_limit WHERE key=?', (key,)).fetchone()[0] <= maximum

    def create(self, token_hash, generation):
        with self.store.connect() as db:
            db.execute('DELETE FROM complimentary_session WHERE checked < ?', (int(self.clock()) - 90 * 86400,))
            db.execute('INSERT INTO complimentary_session VALUES (?,?,?)', (token_hash, generation, int(self.clock())))

    def refresh(self, token_hash, generation):
        with self.store.connect() as db:
            return db.execute('UPDATE complimentary_session SET checked=? WHERE token_hash=? AND generation=? AND checked>=?',
                              (int(self.clock()), token_hash, generation, int(self.clock()) - 90 * 86400)).rowcount == 1

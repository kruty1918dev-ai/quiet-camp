"""Durable complimentary sessions and shared rate limits. Never store raw tokens or IPs."""
import json
import time
from urllib.request import Request, urlopen
from urllib.parse import urlsplit


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


class RedisAccessStore:
    """Upstash REST adapter; the Free database must be selected explicitly by its owner."""
    LIMIT_SCRIPT = "local n=redis.call('INCR',KEYS[1]); if n==1 then redis.call('EXPIRE',KEYS[1],ARGV[1]) end; return n"
    REFRESH_SCRIPT = "if redis.call('GET',KEYS[1])==ARGV[1] then redis.call('EXPIRE',KEYS[1],ARGV[2]); return 1 end; return 0"

    def __init__(self, url, token):
        parsed = urlsplit(url)
        if parsed.scheme != 'https' or not parsed.hostname or not parsed.hostname.endswith('.upstash.io') or parsed.port not in (None, 443) or parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path not in ('', '/') or not token:
            raise ValueError('A configured HTTPS Upstash REST database is required')
        self.url, self.token = url.rstrip('/'), token

    def command(self, *args):
        request = Request(self.url, json.dumps(args).encode(), {'Authorization': 'Bearer ' + self.token, 'Content-Type': 'application/json'}, method='POST')
        with urlopen(request, timeout=5) as response:
            data = json.loads(response.read(8192))
        if 'error' in data or 'result' not in data:
            raise RuntimeError('Access store unavailable')
        return data['result']

    def allow(self, key, maximum, seconds):
        return self.command('EVAL', self.LIMIT_SCRIPT, 1, 'qc:access:limit:' + key, seconds) <= maximum

    def create(self, token_hash, generation):
        if self.command('SET', 'qc:access:session:' + token_hash, generation, 'EX', 90 * 86400) != 'OK':
            raise RuntimeError('Session not saved')

    def refresh(self, token_hash, generation):
        return self.command('EVAL', self.REFRESH_SCRIPT, 1, 'qc:access:session:' + token_hash, generation, 90 * 86400) == 1

    def heartbeat(self):
        if self.command('SET', 'qc:access:heartbeat', '1', 'EX', 172800) != 'OK':
            raise RuntimeError('Access store unavailable')

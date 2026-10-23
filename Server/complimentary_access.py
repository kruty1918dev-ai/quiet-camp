"""Explicit, optional complimentary access. No signup, payment or reviewer detection."""
import hashlib
import hmac
import secrets
import time
from datetime import datetime, timezone


class AccessDenied(ValueError):
    pass


def password_hash(password, salt=None):
    salt = salt or secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac('sha256', password.encode(), bytes.fromhex(salt), 600000).hex()
    return f'pbkdf2-sha256$600000${salt}${digest}'


class ComplimentaryAccess:
    def __init__(self, store, package, product, username='', encoded_password='', clock=time.time):
        self.store, self.package, self.product, self.clock = store, package, product, clock
        self.username, self.encoded_password = username, encoded_password
        self.generation = hashlib.sha256((username + ':' + encoded_password).encode()).hexdigest()
        if username or encoded_password:
            parts = encoded_password.split('$')
            if not username or len(username) > 100 or len(parts) != 4 or parts[:2] != ['pbkdf2-sha256', '600000']:
                raise ValueError('Invalid complimentary account configuration')
            if len(bytes.fromhex(parts[2])) != 16 or len(bytes.fromhex(parts[3])) != 32:
                raise ValueError('Invalid complimentary password hash')
        with store.connect() as db:
            db.execute('CREATE TABLE IF NOT EXISTS complimentary_session (token_hash TEXT PRIMARY KEY, generation TEXT NOT NULL, checked INTEGER NOT NULL)')

    def configured(self):
        if not self.username or not self.encoded_password:
            raise RuntimeError('Complimentary access disabled')

    def grant(self, token):
        return dict(packageName=self.package, productId=self.product, scope='pro', state='Granted',
                    sessionToken=token, validUntilUtc=datetime.fromtimestamp(self.clock() + 86400, timezone.utc).isoformat())

    def sign_in(self, request):
        self.configured()
        if set(request) != {'packageName', 'username', 'password'} or request['packageName'] != self.package:
            raise AccessDenied()
        username, password = request['username'], request['password']
        if not isinstance(username, str) or not isinstance(password, str) or len(username) > 100 or not 1 <= len(password) <= 128:
            raise AccessDenied()
        salt = self.encoded_password.split('$')[2]
        valid_password = hmac.compare_digest(password_hash(password, salt), self.encoded_password)
        if not valid_password or not hmac.compare_digest(username.encode(), self.username.encode()):
            raise AccessDenied()
        token = secrets.token_urlsafe(32)
        with self.store.connect() as db:
            # Sessions unused for 90 days can sign in again with the same reusable credentials.
            db.execute('DELETE FROM complimentary_session WHERE checked < ?', (int(self.clock()) - 90 * 86400,))
            db.execute('INSERT INTO complimentary_session VALUES (?,?,?)',
                       (self.store.receipt(token), self.generation, int(self.clock())))
        return self.grant(token)

    def refresh(self, request):
        self.configured()
        if set(request) != {'packageName', 'sessionToken'} or request['packageName'] != self.package:
            raise AccessDenied()
        token = request['sessionToken']
        if not isinstance(token, str) or len(token) != 43 or not all(c.isascii() and (c.isalnum() or c in '-_') for c in token):
            raise AccessDenied()
        with self.store.connect() as db:
            found = db.execute('SELECT generation FROM complimentary_session WHERE token_hash=?', (self.store.receipt(token),)).fetchone()
            if not found or not hmac.compare_digest(found[0], self.generation):
                raise AccessDenied()
            db.execute('UPDATE complimentary_session SET checked=? WHERE token_hash=?', (int(self.clock()), self.store.receipt(token)))
        return self.grant(token)

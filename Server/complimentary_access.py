"""Explicit, optional complimentary access. No signup, payment or reviewer detection."""
import hashlib
import hmac
import secrets
import time
from datetime import datetime, timezone
from access_store import SqliteAccessStore


class AccessDenied(ValueError):
    pass


class AccessRateLimited(Exception):
    pass


def password_hash(password, salt=None):
    salt = salt or secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac('sha256', password.encode(), bytes.fromhex(salt), 600000).hex()
    return f'pbkdf2-sha256$600000${salt}${digest}'


class ComplimentaryAccess:
    def __init__(self, store, package, product, username='', encoded_password='', clock=time.time, *, session_store=None, explore_published=False):
        self.store, self.package, self.product, self.clock = store, package, product, clock
        self.username, self.encoded_password = username, encoded_password
        self.explore_published = explore_published
        self.generation = hashlib.sha256((username + ':' + encoded_password + ':' + str(explore_published)).encode()).hexdigest()
        if username or encoded_password:
            parts = encoded_password.split('$')
            if not username or len(username) > 100 or len(parts) != 4 or parts[:2] != ['pbkdf2-sha256', '600000']:
                raise ValueError('Invalid complimentary account configuration')
            if len(bytes.fromhex(parts[2])) != 16 or len(bytes.fromhex(parts[3])) != 32:
                raise ValueError('Invalid complimentary password hash')
        self.sessions = session_store or SqliteAccessStore(store, clock)

    def check_rate(self, address, route):
        self.configured()
        # The host supplies the trusted peer address. Never trust a client-supplied ID.
        peer = hmac.new(self.generation.encode(), address.encode(), hashlib.sha256).hexdigest()
        signin = route.endswith('/sign-in')
        if not self.sessions.allow(('signin:' if signin else 'refresh:') + peer, 8 if signin else 120, 600):
            raise AccessRateLimited()
        if signin and not self.sessions.allow('signin:global', 200, 600):
            raise AccessRateLimited()

    def configured(self):
        if not self.username or not self.encoded_password:
            raise RuntimeError('Complimentary access disabled')

    def grant(self, token):
        return dict(packageName=self.package, productId=self.product, scope='pro', state='Granted', explorePublishedContent=self.explore_published,
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
        self.sessions.create(hashlib.sha256(token.encode()).hexdigest(), self.generation)
        return self.grant(token)

    def refresh(self, request):
        self.configured()
        if set(request) != {'packageName', 'sessionToken'} or request['packageName'] != self.package:
            raise AccessDenied()
        token = request['sessionToken']
        if not isinstance(token, str) or len(token) != 43 or not all(c.isascii() and (c.isalnum() or c in '-_') for c in token):
            raise AccessDenied()
        if not self.sessions.refresh(hashlib.sha256(token.encode()).hexdigest(), self.generation):
            raise AccessDenied()
        return self.grant(token)

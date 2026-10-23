"""Complimentary-only Vercel function. Billing, analytics and signup are not exposed."""
from http.server import BaseHTTPRequestHandler
import ipaddress
import hmac
import os
from urllib.parse import parse_qs, urlsplit

from access_store import RedisAccessStore
from complimentary_access import ComplimentaryAccess
from service import wsgi


def create_access():
    if len(os.environ.get('CRON_SECRET', '')) < 32:
        raise ValueError('Scheduled access-store check must be configured')
    return ComplimentaryAccess(None, os.environ['QC_ANDROID_PACKAGE'], os.environ['QC_PRO_PRODUCT'],
                               os.environ['QC_COMPLIMENTARY_USERNAME'], os.environ['QC_COMPLIMENTARY_PASSWORD_HASH'],
                               session_store=RedisAccessStore(os.environ['UPSTASH_REDIS_REST_URL'], os.environ['UPSTASH_REDIS_REST_TOKEN']),
                               explore_published=os.environ.get('QC_COMPLIMENTARY_EXPLORE_PUBLISHED') == 'true')


class handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        # No request URLs, query strings, account names, bodies or bearer tokens.
        pass

    def unavailable(self):
        self.send_response(503)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(b'{"error":"Retry later"}')

    def action(self):
        url = urlsplit(self.path)
        if url.path == '/api/quietcamp-access':
            values = parse_qs(url.query).get('action', [])
            return values[0] if len(values) == 1 else ''
        return url.path[len('/v1/access/'):] if url.path.startswith('/v1/access/') else ''

    def do_GET(self):
        action = self.action()
        if action not in ('health', 'maintenance'):
            self.send_error(404); return
        if action == 'maintenance':
            secret = os.environ.get('CRON_SECRET', '')
            if len(secret) < 32:
                self.unavailable(); return
            if not hmac.compare_digest(self.headers.get('Authorization', '').encode(), ('Bearer '+secret).encode()):
                self.send_response(401)
                self.send_header('Cache-Control', 'no-store')
                self.end_headers(); return
        try:
            access = create_access()
            access.configured()
            if action == 'maintenance': access.sessions.heartbeat()
        except Exception:
            self.unavailable(); return
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(b'{"service":"quietcamp-complimentary-access","schemaVersion":1}')

    def do_POST(self):
        action = self.action()
        if action not in ('sign-in', 'refresh'):
            self.send_error(404); return
        try:
            # Vercel overwrites this header at its trusted ingress. Never use a
            # body field or an arbitrary client-supplied X-Forwarded-For chain.
            address = self.headers.get('x-vercel-forwarded-for', '') if os.environ.get('VERCEL') == '1' else self.client_address[0]
            address = str(ipaddress.ip_address(address))
            access = create_access()
            app = wsgi(None, complimentary=access)
            def start(status, headers):
                self.send_response(int(status.split()[0]))
                for key, value in headers: self.send_header(key, value)
                self.end_headers()
            result = app(dict(PATH_INFO='/v1/access/' + action, REQUEST_METHOD='POST', REMOTE_ADDR=address,
                              CONTENT_TYPE=self.headers.get('Content-Type', ''), CONTENT_LENGTH=self.headers.get('Content-Length', ''),
                              **{'wsgi.input': self.rfile}), start)
            for part in result: self.wfile.write(part)
        except Exception:
            self.unavailable()

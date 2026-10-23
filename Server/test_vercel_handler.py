import contextlib
import io
import json
from http.server import HTTPServer
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from complimentary_access import ComplimentaryAccess, password_hash
from service import Store
import vercel_handler


class VercelHttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.encoded = password_hash('local-http-test-credential')

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.store = Store(Path(self.directory.name)/'sessions.sqlite')
        self.factory = patch.object(vercel_handler, 'create_access', self.access)
        self.factory.start()
        self.environment = patch.dict('os.environ', {'VERCEL':'0'})
        self.environment.start()
        self.server = HTTPServer(('127.0.0.1',0),vercel_handler.handler)
        self.thread = threading.Thread(target=self.server.serve_forever,daemon=True)
        self.thread.start()

    def access(self):
        return ComplimentaryAccess(self.store,'com.quietcamp.game','quietcamp_pro','test-access',self.encoded, explore_published=True)

    def tearDown(self):
        self.server.shutdown(); self.server.server_close(); self.thread.join()
        self.factory.stop(); self.environment.stop(); self.directory.cleanup()

    def call(self, action, data=None, headers=None):
        request = Request('http://127.0.0.1:'+str(self.server.server_port)+'/api/quietcamp-access?action='+action,
                          json.dumps(data).encode() if data is not None else None,
                          {'Content-Type':'application/json', **(headers or {})})
        try: response=urlopen(request,timeout=5)
        except HTTPError as error: response=error
        with response:
            body=response.read()
            return response.status,json.loads(body) if body else {},dict(response.headers)

    def test_real_function_http_persists_sessions_and_never_logs_credentials(self):
        captured=io.StringIO()
        with contextlib.redirect_stderr(captured):
            status,body,_=self.call('health'); self.assertEqual(200,status)
            status,grant,headers=self.call('sign-in',dict(packageName='com.quietcamp.game',username='test-access',password='local-http-test-credential'))
            self.assertEqual(200,status); self.assertTrue(grant['explorePublishedContent']); self.assertEqual('no-store',headers['Cache-Control'])
            status,renewed,_=self.call('refresh',dict(packageName='com.quietcamp.game',sessionToken=grant['sessionToken']))
            self.assertEqual(200,status); self.assertEqual(grant['sessionToken'],renewed['sessionToken'])
        self.assertEqual('',captured.getvalue())

    def test_spoofed_forwarded_for_cannot_bypass_shared_limit(self):
        for index in range(8):
            status,_,_=self.call('sign-in',dict(packageName='com.quietcamp.game',username='test-access',password='wrong'),{'X-Forwarded-For':'192.0.2.'+str(index)})
            self.assertEqual(401,status)
        status,body,headers=self.call('sign-in',dict(packageName='com.quietcamp.game',username='test-access',password='local-http-test-credential'))
        self.assertEqual(429,status); self.assertEqual('600',headers['Retry-After']); self.assertNotIn('sessionToken',body)

    def test_store_failure_and_missing_trusted_vercel_address_fail_closed(self):
        with patch.object(vercel_handler,'create_access',side_effect=RuntimeError('private detail')):
            status,body,_=self.call('sign-in',{})
            self.assertEqual(503,status); self.assertNotIn('private',str(body))
        with patch.dict('os.environ',{'VERCEL':'1'}):
            self.assertEqual(503,self.call('sign-in',{}, {'X-Forwarded-For':'192.0.2.5'})[0])

    def test_scheduled_check_requires_server_secret_and_handles_store_failure(self):
        with patch.dict('os.environ',{'CRON_SECRET':'test-scheduled-secret-'+'a'*32}):
            self.assertEqual(401,self.call('maintenance')[0])
            with patch.object(self.access().sessions.__class__,'heartbeat',create=True,side_effect=RuntimeError('store unavailable')):
                self.assertEqual(503,self.call('maintenance',headers={'Authorization':'Bearer test-scheduled-secret-'+'a'*32})[0])
            with patch.object(self.access().sessions.__class__,'heartbeat',create=True,return_value=None):
                status,body,_=self.call('maintenance',headers={'Authorization':'Bearer test-scheduled-secret-'+'a'*32})
                self.assertEqual(200,status); self.assertNotIn('sessionToken',body)


if __name__ == '__main__': unittest.main()

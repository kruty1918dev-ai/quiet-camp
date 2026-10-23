import io
import json
from pathlib import Path
import tempfile
import unittest
from complimentary_access import ComplimentaryAccess, password_hash
from provision_complimentary_access import TITLE, INSTRUCTIONS, provision
from service import Store, Service, wsgi


class AccessTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.encoded = password_hash('test-password-not-a-production-credential')
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.store = Store(Path(self.directory.name)/'access.sqlite')
        self.now = 1800000000
        self.access = ComplimentaryAccess(self.store, 'com.quietcamp.game', 'quietcamp_pro', 'play-review', self.encoded, lambda: self.now)
        self.app = wsgi(Service(None, self.store), complimentary=self.access)
        self.credentials = dict(packageName='com.quietcamp.game', username='play-review', password='test-password-not-a-production-credential')
    def tearDown(self): self.directory.cleanup()
    def call(self, path, data, app=None):
        encoded = json.dumps(data).encode(); status=[]
        response = b''.join((app or self.app)(dict(PATH_INFO=path, REQUEST_METHOD='POST', CONTENT_TYPE='application/json',
                              CONTENT_LENGTH=str(len(encoded)), **{'wsgi.input':io.BytesIO(encoded)}), lambda s,h: status.append(s)))
        return status[0], json.loads(response)
    def test_reusable_credentials_full_pro_without_play_payment_or_registration(self):
        tokens=[]
        for _ in range(3):
            status, grant = self.call('/v1/access/sign-in', self.credentials)
            self.assertEqual('200 OK',status); self.assertEqual('Granted',grant['state'])
            self.assertEqual(('pro','quietcamp_pro','com.quietcamp.game'),(grant['scope'],grant['productId'],grant['packageName']))
            tokens.append(grant['sessionToken'])
        self.assertEqual(3,len(set(tokens)))
        # Restart does not require a new account, email confirmation, OTP or purchase.
        access = ComplimentaryAccess(Store(self.store.path), 'com.quietcamp.game', 'quietcamp_pro', 'play-review', self.encoded, lambda: self.now)
        result = access.refresh(dict(packageName='com.quietcamp.game',sessionToken=tokens[0]))
        self.assertEqual('Granted',result['state'])
        with self.store.connect() as db: self.assertEqual(0,db.execute('SELECT COUNT(*) FROM entitlement').fetchone()[0])
        raw=Path(self.store.path).read_bytes()
        for secret in [self.credentials['password'], *tokens]: self.assertNotIn(secret.encode(),raw)
    def test_wrong_credentials_tampering_and_cross_package_do_not_grant(self):
        for changes in [dict(password='wrong'),dict(username='unknown'),dict(packageName='other.package'),dict(password='x'*129),dict(extra='scope')]:
            status,body=self.call('/v1/access/sign-in',dict(self.credentials,**changes))
            self.assertEqual('401 Unauthorized',status); self.assertNotIn('sessionToken',body)
        status,_=self.call('/v1/access/refresh',dict(packageName='com.quietcamp.game',sessionToken='x'*43))
        self.assertEqual('401 Unauthorized',status)
    def test_rotation_revokes_sessions_but_not_reusable_new_credentials(self):
        _,grant=self.call('/v1/access/sign-in',self.credentials)
        rotated=ComplimentaryAccess(self.store,'com.quietcamp.game','quietcamp_pro','play-review',password_hash('replacement-password'),lambda:self.now)
        app=wsgi(Service(None,self.store),complimentary=rotated)
        status,_=self.call('/v1/access/refresh',dict(packageName='com.quietcamp.game',sessionToken=grant['sessionToken']),app)
        self.assertEqual('401 Unauthorized',status)
        status,_=self.call('/v1/access/sign-in',dict(self.credentials,password='replacement-password'),app)
        self.assertEqual('200 OK',status)
    def test_disabled_returns_unavailable_without_secret_echo(self):
        app=wsgi(Service(None,self.store),complimentary=ComplimentaryAccess(self.store,'com.quietcamp.game','quietcamp_pro'))
        status,body=self.call('/v1/access/sign-in',self.credentials,app)
        self.assertEqual('503 Service Unavailable',status)
        self.assertNotIn(self.credentials['password'],json.dumps(body))
    def test_credentials_remain_valid_after_old_sessions_are_cleaned_up(self):
        _,old=self.call('/v1/access/sign-in',self.credentials)
        self.now+=365*86400
        status,new=self.call('/v1/access/sign-in',self.credentials)
        self.assertEqual('200 OK',status)
        status,_=self.call('/v1/access/refresh',dict(packageName='com.quietcamp.game',sessionToken=old['sessionToken']))
        self.assertEqual('401 Unauthorized',status)
        status,_=self.call('/v1/access/refresh',dict(packageName='com.quietcamp.game',sessionToken=new['sessionToken']))
        self.assertEqual('200 OK',status)
    def test_provisioning_is_private_non_overwriting_and_within_console_limits(self):
        self.assertLessEqual(len(TITLE),60); self.assertLessEqual(len(INSTRUCTIONS),500)
        path=provision(Path(self.directory.name)/'private')
        data=json.loads(path.read_text(encoding='utf-8'))
        self.assertFalse(data['readyForPlayConsole']); self.assertLessEqual(len(data['password']),100)
        self.assertNotIn(data['password'],path.with_name('server-account.private.env').read_text(encoding='utf-8'))
        with self.assertRaises(ValueError): provision(path.parent)
        with self.assertRaises(ValueError): provision(Path(__file__).resolve().parent/'unsafe')


if __name__ == '__main__': unittest.main()

import base64
import io
import json
import tempfile
import unittest
from pathlib import Path
from service import Service, Store, Rejected, wsgi


class Play:
    def __init__(self): self.state = "PURCHASED"; self.acks = 0; self.fail = False; self.voided_tokens = []
    def get(self, token):
        if self.fail: raise RuntimeError("private token must not appear in response")
        return {"productLineItem": [{"productId": "quietcamp_pro", "quantity": 1}],
                "purchaseStateContext": {"purchaseState": self.state},
                "acknowledgementState": "ACKNOWLEDGEMENT_STATE_PENDING" if not self.acks else "ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED"}
    def acknowledge(self, token): self.acks += 1
    def voided(self, since): return iter(self.voided_tokens)


class ServiceTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.play = Play()
        self.store = Store(Path(self.directory.name) / "entitlements.sqlite")
        self.service = Service(self.play, self.store, clock=lambda: 1800000000)
        self.request = {"packageName": "com.quietcamp.game", "productId": "quietcamp_pro", "purchaseToken": "sensitive-purchase-token-123456"}
    def tearDown(self): self.directory.cleanup()
    def test_owned_ack_idempotency_and_restart(self):
        first = self.service.verify(self.request)
        self.assertEqual("Owned", first["state"])
        self.assertTrue(first["verified"])
        self.assertEqual(first, self.service.verify(self.request))
        self.assertEqual(1, self.play.acks)
        self.assertNotIn(self.request["purchaseToken"], json.dumps(first))
        restarted = Service(self.play, Store(self.store.path), clock=lambda: 1800000000)
        self.assertEqual(first["receiptId"], restarted.verify(self.request)["receiptId"])
    def test_pending_never_grants_or_acknowledges(self):
        self.play.state = "PENDING"
        result = self.service.verify(self.request)
        self.assertFalse(result["verified"]); self.assertEqual("Pending", result["state"])
        self.assertEqual(0, self.play.acks)
        self.play.state = "PURCHASED"
        self.assertEqual("Owned", self.service.verify(self.request)["state"])
    def test_cancel_refund_terminal_race(self):
        self.service.verify(self.request)
        self.play.state = "CANCELLED"
        self.assertEqual("Revoked", self.service.verify(self.request)["state"])
        self.play.state = "PURCHASED"
        self.assertEqual("Revoked", self.service.verify(self.request)["state"])
        self.assertEqual("Revoked", self.store.record(self.request["purchaseToken"], "quietcamp_pro", "Owned", 1800000000))
    def test_voided_and_duplicate_notification(self):
        self.play.voided_tokens = [self.request["purchaseToken"]]
        self.assertEqual(1, self.service.reconcile(1800000000 - 3600))
        self.assertEqual(1, self.service.reconcile(1800000000 - 3600))
        self.assertEqual("Revoked", self.service.verify(self.request)["state"])
        with self.assertRaises(Rejected): self.service.reconcile(0)
    def test_reject_wrong_product_package_unknown_fields(self):
        for key, value in (("packageName", "other"), ("productId", "coins"), ("private_key", "secret"), ("purchaseToken", "tiny")):
            bad = dict(self.request); bad[key] = value
            with self.assertRaises(Rejected): self.service.verify(bad)
    def test_metrics_no_identifiers_and_separate_legal_gate(self):
        request = {"name": "level_result", "bucket": 1, "count": 2}
        with self.assertRaises(Rejected): self.service.metrics(request)
        self.service.metrics_reviewed = True
        self.service.metrics(request)
        for key in ("deviceId", "age", "userId", "ip", "consent"):
            with self.assertRaises(Rejected): self.service.metrics(dict(request, **{key: "x"}))
        with self.store.connect() as db:
            self.assertEqual(2, db.execute("SELECT count FROM metrics").fetchone()[0])
    def call(self, app, body, route="/v1/purchases/verify", auth=""):
        data = json.dumps(body).encode(); status = []
        result = app({"REQUEST_METHOD": "POST", "PATH_INFO": route, "CONTENT_TYPE": "application/json", "CONTENT_LENGTH": str(len(data)),
                      "wsgi.input": io.BytesIO(data), "HTTP_AUTHORIZATION": auth}, lambda code, headers: status.append(code))
        return status[0], b"".join(result).decode()
    def test_network_failure_never_echoes_token(self):
        self.play.fail = True
        code, body = self.call(wsgi(self.service), self.request)
        self.assertEqual("503 Service Unavailable", code)
        self.assertNotIn("private", body)
    def test_notifications_require_verified_oidc(self):
        payload = {"packageName": "com.quietcamp.game", "voidedPurchaseNotification": {"productType": 2, "purchaseToken": self.request["purchaseToken"]}}
        message = {"message": {"data": base64.b64encode(json.dumps(payload).encode()).decode()}}
        self.assertEqual("400 Bad Request", self.call(wsgi(self.service), message, "/v1/play/notifications")[0])
        def verify(header):
            if header != "Bearer checked-identity": raise Rejected()
        app = wsgi(self.service, verify)
        self.assertEqual("400 Bad Request", self.call(app, message, "/v1/play/notifications")[0])
        self.assertEqual("200 OK", self.call(app, message, "/v1/play/notifications", "Bearer checked-identity")[0])
        self.assertTrue(self.store.revoked(self.request["purchaseToken"]))


if __name__ == "__main__": unittest.main()

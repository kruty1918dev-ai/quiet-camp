"""Google Play verification and bounded metrics. WSGI app; no deployment side effects."""
from contextlib import contextmanager
import base64
import hashlib
import json
import os
import re
import sqlite3
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import quote


class Rejected(ValueError):
    pass


class GooglePlay:
    def __init__(self, package, product):
        import google.auth
        from google.auth.transport.requests import AuthorizedSession
        credentials, _ = google.auth.default(scopes=["https://www.googleapis.com/auth/androidpublisher"])
        self.http = AuthorizedSession(credentials)
        self.base = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/" + quote(package, safe="")
        self.product = product

    def get(self, token):
        response = self.http.get(self.base + "/purchases/productsv2/tokens/" + quote(token, safe=""), timeout=15)
        if response.status_code in (400, 404, 410): raise Rejected("Unknown purchase")
        if response.status_code != 200: raise RuntimeError("Play verification unavailable")
        return response.json()

    def acknowledge(self, token):
        response = self.http.post(self.base + "/purchases/products/" + quote(self.product, safe="") +
                                  "/tokens/" + quote(token, safe="") + ":acknowledge", json={}, timeout=15)
        if response.status_code not in (200, 204): raise RuntimeError("Acknowledgment unavailable")

    def voided(self, since):
        page = None
        while True:
            params = {"startTime": str(int(since * 1000)), "type": 0, "maxResults": 1000}
            if page: params["token"] = page
            response = self.http.get(self.base + "/purchases/voidedpurchases", params=params, timeout=15)
            if response.status_code != 200: raise RuntimeError("Voided purchase query unavailable")
            body = response.json()
            for purchase in body.get("voidedPurchases", []): yield purchase["purchaseToken"]
            page = body.get("tokenPagination", {}).get("nextPageToken")
            if not page: break


class Store:
    def __init__(self, path):
        self.path = str(path)
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        with self.connect() as db:
            db.execute("CREATE TABLE IF NOT EXISTS entitlement (receipt TEXT PRIMARY KEY, product TEXT NOT NULL, state TEXT NOT NULL, checked INTEGER NOT NULL)")
            db.execute("CREATE TABLE IF NOT EXISTS metrics (day TEXT, name TEXT, bucket INTEGER, count INTEGER NOT NULL, PRIMARY KEY(day,name,bucket))")
        try: os.chmod(path, 0o600)
        except OSError: pass

    @contextmanager
    def connect(self):
        connection = sqlite3.connect(self.path, timeout=10)
        connection.execute("PRAGMA journal_mode=WAL")
        try:
            with connection: yield connection
        finally: connection.close()

    @staticmethod
    def receipt(token): return hashlib.sha256(token.encode()).hexdigest()

    def revoked(self, token):
        with self.connect() as db:
            row = db.execute("SELECT state FROM entitlement WHERE receipt=?", (self.receipt(token),)).fetchone()
        return bool(row and row[0] == "Revoked")

    def record(self, token, product, state, now):
        receipt = self.receipt(token)
        with self.connect() as db:
            # A terminal revocation wins races against a concurrent positive Google response.
            db.execute("INSERT INTO entitlement VALUES (?,?,?,?) ON CONFLICT(receipt) DO UPDATE SET state=CASE WHEN entitlement.state='Revoked' THEN 'Revoked' ELSE excluded.state END, checked=excluded.checked",
                       (receipt, product, state, int(now)))
            row = db.execute("SELECT state FROM entitlement WHERE receipt=?", (receipt,)).fetchone()
        return row[0]


class Service:
    def __init__(self, play, store, package="com.quietcamp.game", product="quietcamp_pro", metrics_reviewed=False, clock=time.time):
        self.play, self.store = play, store
        self.package, self.product, self.metrics_reviewed, self.clock = package, product, metrics_reviewed, clock

    def verify(self, request):
        if set(request) != {"packageName", "productId", "purchaseToken"}: raise Rejected("Invalid request fields")
        if request["packageName"] != self.package or request["productId"] != self.product: raise Rejected("Product mismatch")
        token = request["purchaseToken"]
        if not isinstance(token, str) or not 16 <= len(token) <= 4096 or any(c.isspace() for c in token): raise Rejected("Invalid token")
        now = self.clock()
        if self.store.revoked(token): state = "Revoked"
        else:
            purchase = self.play.get(token)
            lines = purchase.get("productLineItem", [])
            if len(lines) != 1 or lines[0].get("productId") != self.product: raise Rejected("Verified product mismatch")
            if lines[0].get("quantity", 1) != 1: raise Rejected("Unsupported quantity")
            status = purchase.get("purchaseStateContext", {}).get("purchaseState")
            if status == "PENDING": state = "Pending"
            elif status == "CANCELLED": state = "Revoked"
            elif status == "PURCHASED":
                state = "Owned"
                # Durable server grant precedes acknowledgment; client retries read it.
                state = self.store.record(token, self.product, state, now)
                if state == "Owned" and purchase.get("acknowledgementState") != "ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED":
                    self.play.acknowledge(token)
            else: raise Rejected("Unknown Google purchase state")
        state = self.store.record(token, self.product, state, now)
        return {"productId": self.product, "receiptId": self.store.receipt(token), "state": state,
                "verified": state in ("Owned", "Revoked"),
                "validUntilUtc": datetime.fromtimestamp(now + 86400, timezone.utc).isoformat() if state == "Owned" else ""}

    def revoke(self, token): self.store.record(token, self.product, "Revoked", self.clock())

    def reconcile(self, since):
        if since < self.clock() - 30 * 86400 or since > self.clock(): raise Rejected("Use a rolling window within 30 days")
        count = 0
        for token in self.play.voided(since): self.revoke(token); count += 1
        return count

    def notification(self, payload):
        if payload.get("packageName") != self.package: raise Rejected("Package mismatch")
        voided = payload.get("voidedPurchaseNotification")
        event = payload.get("oneTimeProductNotification")
        if voided:
            token = voided["purchaseToken"]
            if voided.get("productType") != 2: return
            self.revoke(token)
        elif event:
            if event.get("sku") != self.product: raise Rejected("Product mismatch")
            # Notifications are hints. Re-query Play; never trust a claimed purchase state.
            self.verify({"packageName": self.package, "productId": self.product, "purchaseToken": event["purchaseToken"]})

    def metrics(self, request):
        if not self.metrics_reviewed: raise Rejected("Metrics disabled pending legal basis review")
        if set(request) != {"name", "bucket", "count"}: raise Rejected("Metrics fields not allowed")
        if request["name"] not in ("level_result", "technical_failure", "frame_time"): raise Rejected("Metric not allowed")
        if type(request["bucket"]) is not int or not 0 <= request["bucket"] <= 5: raise Rejected("Invalid bucket")
        if type(request["count"]) is not int or not 1 <= request["count"] <= 100: raise Rejected("Invalid count")
        day = datetime.fromtimestamp(self.clock(), timezone.utc).date().isoformat()
        cutoff = datetime.fromtimestamp(self.clock() - 30 * 86400, timezone.utc).date().isoformat()
        with self.store.connect() as db:
            db.execute("INSERT INTO metrics VALUES (?,?,?,?) ON CONFLICT(day,name,bucket) DO UPDATE SET count=MIN(1000000000,metrics.count+excluded.count)",
                       (day, request["name"], request["bucket"], request["count"]))
            db.execute("DELETE FROM metrics WHERE day<?", (cutoff,))


def wsgi(service, oidc_verify=None):
    def application(environ, start_response):
        status, body = "200 OK", {}
        try:
            if environ.get("REQUEST_METHOD") != "POST": raise Rejected("POST required")
            size = int(environ.get("CONTENT_LENGTH") or 0)
            if size < 2 or size > 16384: raise Rejected("Invalid body size")
            if environ.get("CONTENT_TYPE", "").split(";")[0] != "application/json": raise Rejected("JSON required")
            request = json.loads(environ["wsgi.input"].read(size))
            if not isinstance(request, dict): raise Rejected("Object required")
            route = environ.get("PATH_INFO")
            if route == "/v1/purchases/verify": body = service.verify(request)
            elif route == "/v1/metrics": service.metrics(request)
            elif route == "/v1/play/notifications":
                # Pub/Sub push OIDC is mandatory, checked before decoding data.
                if oidc_verify is None: raise Rejected("Notifications not configured")
                oidc_verify(environ.get("HTTP_AUTHORIZATION", ""))
                message = request["message"]
                data = json.loads(base64.b64decode(message["data"], validate=True))
                service.notification(data)
            else: status = "404 Not Found"
        except (Rejected, ValueError, KeyError, TypeError):
            status, body = "400 Bad Request", {"error": "Request rejected"}
        except Exception:
            # Never echo Google exceptions: they can include purchase-token URLs.
            status, body = "503 Service Unavailable", {"error": "Retry later"}
        encoded = json.dumps(body, separators=(",", ":")).encode()
        start_response(status, [("Content-Type", "application/json"), ("Cache-Control", "no-store"), ("Content-Length", str(len(encoded)))])
        return [encoded]
    return application


def production_app():
    package = os.environ["QC_ANDROID_PACKAGE"]
    product = os.environ["QC_PRO_PRODUCT"]
    store = Store(os.environ["QC_DATABASE_PATH"])
    service = Service(GooglePlay(package, product), store, package, product,
                      metrics_reviewed=os.environ.get("QC_METRICS_LEGAL_BASIS_REVIEWED") == "true")
    audience = os.environ.get("QC_PUBSUB_AUDIENCE")
    identity = os.environ.get("QC_PUBSUB_SERVICE_ACCOUNT_EMAIL")
    def verify_oidc(header):
        if not audience or not identity or not header.startswith("Bearer "): raise Rejected("OIDC required")
        from google.oauth2.id_token import verify_oauth2_token
        from google.auth.transport.requests import Request
        try: claims = verify_oauth2_token(header[7:], Request(), audience=audience)
        except Exception: raise Rejected("OIDC rejected") from None
        if claims.get("email") != identity or claims.get("email_verified") is not True: raise Rejected("Wrong push identity")
    return wsgi(service, verify_oidc)


# Lazy initialization allows importing and testing without credentials or network.
_app, _lock = None, threading.Lock()
def application(environ, start_response):
    global _app
    with _lock:
        if _app is None: _app = production_app()
    return _app(environ, start_response)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("reconcile",))
    parser.add_argument("--hours", type=int, default=48)
    args = parser.parse_args()
    # A scheduled server job, never invoked by client configuration preparation.
    package, product = os.environ["QC_ANDROID_PACKAGE"], os.environ["QC_PRO_PRODUCT"]
    service = Service(GooglePlay(package, product), Store(os.environ["QC_DATABASE_PATH"]), package, product)
    print("Revocations reconciled:", service.reconcile(time.time() - args.hours * 3600))

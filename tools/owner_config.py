#!/usr/bin/env python3
"""Owner configuration: validate, preview, apply, readiness. Never launches Unity."""
import argparse
import copy
import hashlib
import json
import os
import re
import sys
import tempfile
from datetime import date
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
RESOURCE = "QuietCamp/Assets/QuietCamp/Resources/QuietCamp/"
SAMPLE_PUBLISHER = "3940256099942544"
APP = re.compile(r"ca-app-pub-(\d{16})~\d{10}\Z")
UNIT = re.compile(r"ca-app-pub-(\d{16})/\d{10}\Z")
PACKAGE = re.compile(r"[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+\Z")
SECRET = re.compile(r"private.?key|password|secret|access.?token|refresh.?token|purchase.?token|keystore|service.?account|credential|signing.?key|authorization", re.I)


class ConfigError(ValueError):
    pass


def read(path):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ConfigError("Duplicate JSON property")
            result[key] = value
        return result
    try:
        return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=pairs)
    except (OSError, ValueError):
        raise ConfigError("Cannot read valid JSON: " + Path(path).name) from None


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest() if data is not None else None


def https(value):
    try:
        uri = urlsplit(value)
        host = uri.hostname or ""
        return uri.scheme == "https" and bool(host) and "." in host and not uri.username and not uri.password \
            and not uri.fragment and not any(x in host.lower() for x in ("localhost", "example.", ".invalid", ".test")) \
            and not any(x in value.lower() for x in ("placeholder", "changeme", "{{", "}}"))
    except ValueError:
        return False


def validate(config):
    try:
        return validate_fields(config)
    except (TypeError, AttributeError, KeyError, IndexError):
        return ["owner: malformed nested configuration or Firebase document"]


def validate_fields(config):
    template = read(ROOT / "Config/owner.example.json")
    errors = []
    def fail(path, message):
        errors.append(path + ": " + message)
    def walk(value, model, path="owner"):
        if isinstance(model, dict):
            if not isinstance(value, dict):
                fail(path, "expected object"); return
            for key in value:
                if key not in model:
                    fail(path, "unknown property (not allowed)")
            for key in model:
                if key not in value:
                    fail(path + "." + key, "required property; use an empty value to disable")
                elif key == "androidGoogleServices":
                    if value[key] is not None and not isinstance(value[key], dict):
                        fail(path + "." + key, "expected official object or null")
                else:
                    walk(value[key], model[key], path + "." + key)
        elif type(value) is not type(model):
            fail(path, "wrong JSON type")
    walk(config, template)
    def secrets(value):
        if isinstance(value, dict):
            for key, item in value.items():
                if SECRET.search(key):
                    fail("owner", "server credential property is forbidden")
                secrets(item)
        elif isinstance(value, list):
            for item in value: secrets(item)
        elif isinstance(value, str) and "PRIVATE KEY" in value:
            fail("owner", "private key material is forbidden")
    secrets(config)
    if errors:
        return errors
    if config["schemaVersion"] != 1: fail("schemaVersion", "unsupported version")
    if config["mode"] not in ("development", "production"): fail("mode", "development or production required")
    package = config["android"]["packageName"]
    if not PACKAGE.fullmatch(package): fail("android.packageName", "invalid Android application ID")
    ads = config["admob"]
    publishers = set()
    for key in ("appId", "rewardedLifeUnitId", "rewardedHintUnitId", "interstitialUnitId"):
        value = ads[key]
        if not value: continue
        match = (APP if key == "appId" else UNIT).fullmatch(value)
        if not match: fail("admob." + key, "expected App ID (~) or ad-unit ID (/) of the correct kind")
        else:
            publishers.add(match[1])
            if len(set(match[1])) == 1 or len(set(value[-10:])) == 1:
                fail("admob." + key, "placeholder numeric ID")
            if config["mode"] == "production" and match[1] == SAMPLE_PUBLISHER:
                fail("admob." + key, "sample inventory forbidden in production")
    if any(ads[k] for k in ("rewardedLifeUnitId", "rewardedHintUnitId", "interstitialUnitId")) and not ads["appId"]:
        fail("admob.appId", "required when an ad unit is configured")
    if ads["publisherId"]:
        if not re.fullmatch(r"pub-\d{16}", ads["publisherId"]): fail("admob.publisherId", "expected pub- plus 16 digits")
        else: publishers.add(ads["publisherId"][4:])
    if len(publishers) > 1: fail("admob", "publisher IDs disagree")
    fb = config["firebase"]
    if fb["projectId"] and not re.fullmatch(r"[a-z][a-z0-9-]{4,28}[a-z0-9]", fb["projectId"]):
        fail("firebase.projectId", "invalid Firebase project ID")
    services = fb["androidGoogleServices"]
    if services is not None:
        if not isinstance(services.get("project_info"), dict) or not isinstance(services.get("client"), list):
            fail("firebase", "expected official project_info and client array")
            return errors
        if not fb["projectId"] or services.get("project_info", {}).get("project_id") != fb["projectId"]:
            fail("firebase", "project_info.project_id mismatch")
        clients = [c for c in services.get("client", []) if isinstance(c, dict) and isinstance(c.get("client_info"), dict) and
                   isinstance(c["client_info"].get("android_client_info"), dict) and
                   c.get("client_info", {}).get("android_client_info", {}).get("package_name") == package]
        if len(clients) != 1:
            fail("firebase", "exactly one Android client must match android.packageName")
        else:
            if not re.fullmatch(r"1:\d+:android:[0-9a-f]+", clients[0].get("client_info", {}).get("mobilesdk_app_id", "")):
                fail("firebase", "missing valid Android mobilesdk_app_id")
            if not isinstance(clients[0].get("api_key"), list) or not any(isinstance(k.get("current_key"), str) and re.fullmatch(r"AIza[\w-]{35}", k.get("current_key", "")) for k in clients[0].get("api_key", []) if isinstance(k, dict)):
                fail("firebase", "missing valid client API key")
            if not str(services.get("project_info", {}).get("project_number", "")).isdigit():
                fail("firebase", "missing project_number")
            elif clients[0]["client_info"]["mobilesdk_app_id"].split(":")[1] != str(services["project_info"]["project_number"]):
                fail("firebase", "Android app ID and project_number mismatch")
    for section, keys in (("billing", ("verificationEndpoint",)), ("operationalMetrics", ("endpoint",)),
                          ("legal", ("privacyPolicyUrl", "supportUrl", "termsUrl", "dataRequestUrl"))):
        for key in keys:
            if config[section][key] and not https(config[section][key]): fail(section + "." + key, "use a real HTTPS URL without credentials or fragments")
    billing = config["billing"]
    if billing["proProductId"] and not re.fullmatch(r"[a-z0-9][a-z0-9_.]{0,147}", billing["proProductId"]):
        fail("billing.proProductId", "invalid Play product ID")
    if billing["verificationEndpoint"] and not billing["proProductId"]: fail("billing", "endpoint requires product ID")
    legal = config["legal"]
    if not 0 <= legal["minimumAge"] <= 18: fail("legal.minimumAge", "range is 0..18")
    for key in ("supportEmail", "privacyEmail"):
        if legal[key] and not re.fullmatch(r"[^\s@]+@[^\s@]+\.[^\s@]+", legal[key]): fail("legal." + key, "invalid email")
    if legal["publishedOn"]:
        try: date.fromisoformat(legal["publishedOn"])
        except ValueError: fail("legal.publishedOn", "expected yyyy-mm-dd")
    if config["reviews"]["policyPublished"] and not all(legal[k] for k in
            ("publisherName", "supportEmail", "privacyPolicyUrl", "dataRequestUrl", "publishedPolicyRevision", "publishedOn")):
        fail("reviews.policyPublished", "publication metadata incomplete")
    if config["reviews"]["childAds"] and not config["reviews"]["childAdFormatsAndroid"]:
        fail("reviews.childAds", "requires actual child-format Android evidence")
    for key, value in config["monetization"].items():
        if value < (1 if key == "completedLevelsBetweenInterstitials" else 0) or value > 86400:
            fail("monetization." + key, "out of supported range")
    return errors


def native_firebase(config):
    source = config["firebase"]["androidGoogleServices"]
    if source is None: return None
    client = next(c for c in source["client"] if c.get("client_info", {}).get("android_client_info", {}).get("package_name") == config["android"]["packageName"])
    # Only the selected Android application's required native client fields.
    # Arbitrary owner/Firebase extension fields never become build payload.
    info = source["project_info"]
    return {"project_info": {key: info[key] for key in ("project_number", "project_id", "storage_bucket", "firebase_url") if key in info},
            "client": [{"client_info": {"mobilesdk_app_id": client["client_info"]["mobilesdk_app_id"],
                                        "android_client_info": {"package_name": config["android"]["packageName"]}},
                        "api_key": [{"current_key": item["current_key"]} for item in client["api_key"] if "current_key" in item]}],
            "configuration_version": "1"}


def planned(config, root=ROOT):
    """Explicit client allow-list; never copies the owner object into Assets."""
    outputs = {}
    def merged(path, values):
        current = read(root / path)
        current.update(values)
        outputs[path] = encoded(current)
    fb = config["firebase"]["androidGoogleServices"]
    ads = config["admob"]
    review = config["reviews"]
    legal = config["legal"]
    merged(RESOURCE + "google_services.json", {
        "analyticsEnabled": fb is not None, "crashlyticsEnabled": fb is not None,
        "rewardedAdsEnabled": bool(ads["appId"] and (ads["rewardedLifeUnitId"] or ads["rewardedHintUnitId"])),
        "useTestAds": config["mode"] == "development", "configurationPrepared": True,
        "androidRewardedUnitId": ads["rewardedLifeUnitId"], "androidHintUnitId": ads["rewardedHintUnitId"],
        "androidInterstitialUnitId": ads["interstitialUnitId"],
        "interstitialsEnabled": bool(ads["appId"] and ads["interstitialUnitId"]),
        "iosRewardedUnitId": "", "privacyPolicyUrl": legal["privacyPolicyUrl"], "deletionRequestUrl": legal["dataRequestUrl"],
        "limitedAdsEnabled": review["limitedAds"], "underAgeLimitedAdsEnabled": review["underAgeAds"],
        "childAdsEnabled": review["childAds"] and review["childAdFormatsAndroid"],
        "adultPersonalizationEnabled": review["adultPersonalization"]})
    fields = {k: legal[k] for k in ("publisherName", "supportEmail", "privacyEmail", "privacyPolicyUrl", "dataRequestUrl", "supportUrl", "termsUrl", "publishedOn", "minimumAge")}
    fields.update(revision=legal["publishedPolicyRevision"], policyIsDraft=not review["policyPublished"],
                  sdkDisclosuresReviewed=review["sdkDisclosures"], teenPrivacyReviewed=review["teenPrivacy"],
                  limitedAdsReviewed=review["limitedAds"], underAgeAdvertisingReviewed=review["underAgeAds"],
                  childAdvertisingReviewed=review["childAds"] and review["childAdFormatsAndroid"],
                  adultPersonalizationReviewed=review["adultPersonalization"])
    merged(RESOURCE + "legal.json", fields)
    outputs[RESOURCE + "owner_client.json"] = encoded({
        "schemaVersion": 1, "mode": config["mode"], "androidPackageName": config["android"]["packageName"],
        "proProductId": config["billing"]["proProductId"], "verificationEndpoint": config["billing"]["verificationEndpoint"],
        "billingReviewed": review["billingTerms"],
        "operationalMetricsEndpoint": config["operationalMetrics"]["endpoint"],
        "operationalMetricsReviewed": review["operationalMetricsLegalBasis"], **config["monetization"]})
    # Null removes previously generated Firebase config. Unknown files are never deleted without a preview hash.
    outputs["QuietCamp/Assets/google-services.json"] = encoded(native_firebase(config)) if fb is not None else None
    settings = "QuietCamp/Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset"
    source = (root / settings).read_text(encoding="utf-8")
    # The SDK provider needs its App ID before C#. When ads are unconfigured,
    # remove the provider rather than supplying a production sample ID.
    app_id = ads["appId"]
    source, count = re.subn(r"(?m)^  adMobAndroidAppId:.*$", "  adMobAndroidAppId:" + (" " + app_id if app_id else ""), source)
    if count != 1: raise ConfigError("Cannot locate SDK App ID field")
    source = re.sub(r"(?m)^  adMobIOSAppId:.*$", "  adMobIOSAppId:", source)
    outputs[settings] = source.encode("utf-8")
    manifest = "QuietCamp/Assets/Plugins/Android/QuietCampPrivacy.androidlib/AndroidManifest.xml"
    source = (root / manifest).read_text(encoding="utf-8")
    source = re.sub(r'(?m)^.*<provider android:name="com.google.android.gms.ads.MobileAdsInitProvider" tools:node="remove" />\s*\n', '', source)
    if not app_id:
        source = source.replace("  <application>", '  <application>\n    <provider android:name="com.google.android.gms.ads.MobileAdsInitProvider" tools:node="remove" />')
    outputs[manifest] = source.encode("utf-8")
    project = "QuietCamp/ProjectSettings/ProjectSettings.asset"
    source = (root / project).read_text(encoding="utf-8")
    source, count = re.subn(r"(?m)^(  applicationIdentifier:\n    Android: ).*$", r"\g<1>" + config["android"]["packageName"], source)
    if count != 1: raise ConfigError("Cannot locate Android applicationIdentifier")
    outputs[project] = source.encode("utf-8")
    return outputs


def snapshot(root, outputs):
    return {path: digest((root / path).read_bytes()) if (root / path).exists() else None for path in outputs}


def plan(config, root=ROOT):
    outputs = planned(config, root)
    changed = {}
    for path, data in outputs.items():
        if path.endswith(".json") and data is not None:
            before = read(root / path) if (root / path).exists() else {}
            after = json.loads(data)
            changed[path] = sorted(key for key in set(before) | set(after) if before.get(key) != after.get(key))
        else:
            changed[path] = ["native SDK settings"]
    return {"version": 1, "configHash": digest(encoded(config)), "before": snapshot(root, outputs),
            "after": {p: digest(b) for p, b in outputs.items()}, "changedFields": changed}


def atomic(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temp = tempfile.mkstemp(dir=path.parent, prefix=".owner-")
    try:
        with os.fdopen(fd, "wb") as file: file.write(data)
        os.replace(temp, path)
    finally:
        if os.path.exists(temp): os.unlink(temp)


def apply(config, preview, root=ROOT):
    current = plan(config, root)
    if current != preview:
        raise ConfigError("Preview is stale or files changed. Run prepare again and review it.")
    outputs = planned(config, root)
    for path, data in outputs.items():
        if data is None:
            (root / path).unlink(missing_ok=True)
        elif digest(data) != current["before"][path]:
            atomic(root / path, data)


def readiness(config):
    r = config["reviews"]
    published = r["policyPublished"]
    common = published and r["sdkDisclosures"]
    ads = config["admob"]
    fb = config["firebase"]["androidGoogleServices"] is not None
    return {
        "advertising": {"configured": bool(ads["appId"] and (ads["rewardedLifeUnitId"] or ads["rewardedHintUnitId"] or ads["interstitialUnitId"])),
                        "reviewed": common, "missingEvidence": ["SDK import, Android UMP, LTD, background, reward persistence, real creatives, revenue; teen/child review is separate"]},
        "analytics": {"configured": fb, "reviewed": common, "missingEvidence": ["Android consent and network capture, Firebase DebugView"]},
        "diagnostics": {"configured": fb, "reviewed": common, "missingEvidence": ["Android crash/restart, queued send/delete, independent consent"]},
        "purchases": {"configured": bool(config["billing"]["verificationEndpoint"] and config["billing"]["proProductId"]),
                      "reviewed": published and r["billingTerms"], "missingEvidence": ["Deploy backend, configure Play product, license tester pending/restore/refund/acknowledgment"]},
        "policy": {"configured": bool(config["legal"]["publishedPolicyRevision"]), "reviewed": published,
                   "missingEvidence": ["Owner/legal review, public policy matches bundled uk/en/de, Play Data Safety submission"]},
        "operationalMetrics": {"configured": bool(config["operationalMetrics"]["endpoint"]), "reviewed": published and r["operationalMetricsLegalBasis"],
                               "missingEvidence": ["Legal basis, retention, ingress log/identifier audit"]}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("validate", "prepare", "apply", "readiness"))
    parser.add_argument("--config", type=Path, default=ROOT / "Config/owner.local.json")
    parser.add_argument("--preview", type=Path, default=ROOT / "Temp/owner-preview.json")
    args = parser.parse_args()
    try:
        config = read(args.config)
        errors = validate(config)
        if errors:
            for error in errors: print("ERROR " + error, file=sys.stderr)
            return 1
        if args.command == "validate": print("VALID: client configuration only; no legal or Android approval.")
        elif args.command == "readiness": print(json.dumps(readiness(config), indent=2, ensure_ascii=False))
        elif args.command == "prepare":
            preview = plan(config)
            atomic(args.preview, encoded(preview))
            for path, before in preview["before"].items():
                after = preview["after"][path]
                print(("UNCHANGED " if before == after else "DELETE " if after is None else "UPDATE ") + path)
                if before != after and preview["changedFields"][path]: print("  fields: " + ", ".join(preview["changedFields"][path]))
            print("Preview contains hashes only. Review; then use apply with the same --config and --preview.")
        else:
            apply(config, read(args.preview))
            print("APPLIED: no Unity/build/deployment; inspect git diff and import in an authorized Editor session.")
        return 0
    except ConfigError as error:
        print("ERROR: " + str(error), file=sys.stderr)
        return 1
    except OSError:
        print("ERROR: inaccessible configuration or output file. No configuration values logged.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

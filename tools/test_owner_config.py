import copy
import json
import tempfile
import unittest
from pathlib import Path
import owner_config as owner


class OwnerTests(unittest.TestCase):
    def setUp(self): self.config = owner.read(owner.ROOT / "Config/owner.example.json")
    def test_empty_partial_corrupt_duplicate(self):
        self.assertEqual([], owner.validate(self.config))
        self.assertTrue(owner.validate({}))
        self.assertTrue(owner.validate([]))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "owner.json"
            for content in ('{', '{"mode":"development","mode":"production"}'):
                path.write_text(content)
                with self.assertRaises(owner.ConfigError): owner.read(path)
    def test_kind_publisher_and_sample(self):
        a = self.config["admob"]
        a["appId"] = "ca-app-pub-3940256099942544/5224354917"
        self.assertTrue(owner.validate(self.config))
        a["appId"] = "ca-app-pub-3940256099942544~3347511713"
        a["rewardedLifeUnitId"] = "ca-app-pub-3940256099942544/5224354917"
        self.assertFalse(owner.validate(self.config))
        self.config["mode"] = "production"
        self.assertTrue(owner.validate(self.config))
        self.config["mode"] = "development"
        a["publisherId"] = "pub-1234567890123456"
        self.assertTrue(owner.validate(self.config))
    def test_firebase_official_object_matching(self):
        fb = self.config["firebase"]
        fb["androidGoogleServices"] = {
            "project_info": {"project_id": "quiet-camp", "project_number": "123456"},
            "client": [{"client_info": {"mobilesdk_app_id": "1:123456:android:abcdef", "android_client_info": {"package_name": "com.quietcamp.game"}},
                        "api_key": [{"current_key": "AIza" + "a" * 35}]}]}
        self.assertFalse(owner.validate(self.config))
        fb["projectId"] = "wrong-project"
        self.assertTrue(owner.validate(self.config))
        fb["projectId"] = "quiet-camp"
        self.config["android"]["packageName"] = "com.other.game"
        self.assertTrue(owner.validate(self.config))
        for bad in ({}, {"project_info": None, "client": []}, {"project_info": {}, "client": "bad"}):
            fb["androidGoogleServices"] = bad
            self.assertTrue(owner.validate(self.config))
    def test_unknown_secrets_types_reviews(self):
        for key in ("private_key", "password", "serverSecret"):
            c = copy.deepcopy(self.config); c[key] = "hidden"
            self.assertTrue(owner.validate(c))
        self.config["schemaVersion"] = True
        self.assertTrue(owner.validate(self.config))
        self.config["schemaVersion"] = 1
        self.config["reviews"]["policyPublished"] = True
        self.assertTrue(owner.validate(self.config))
    def test_preview_detects_manual_changes_and_config_change(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = [owner.RESOURCE + "google_services.json", owner.RESOURCE + "legal.json",
                     "QuietCamp/Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset",
                     "QuietCamp/ProjectSettings/ProjectSettings.asset",
                     "QuietCamp/Assets/Plugins/Android/QuietCampPrivacy.androidlib/AndroidManifest.xml"]
            for path in paths:
                (root / path).parent.mkdir(parents=True, exist_ok=True)
                (root / path).write_bytes((owner.ROOT / path).read_bytes())
            preview = owner.plan(self.config, root)
            path = root / paths[0]
            path.write_text(path.read_text() + " ")
            with self.assertRaises(owner.ConfigError): owner.apply(self.config, preview, root)
            preview = owner.plan(self.config, root)
            owner.apply(self.config, preview, root)
            generated = json.loads((root / (owner.RESOURCE + "owner_client.json")).read_text())
            self.assertNotIn("firebase", generated)
            self.assertFalse(json.loads(path.read_text())["rewardedAdsEnabled"])
            self.assertIn("MobileAdsInitProvider", (root / paths[-1]).read_text())
            self.assertNotIn(owner.SAMPLE_PUBLISHER, (root / paths[2]).read_text())
            self.assertFalse((root / "QuietCamp/Assets/google-services.json").exists())
            stable = owner.plan(self.config, root)
            self.assertEqual(stable["before"], stable["after"])
    def test_readiness_never_claims_android_pass(self):
        for state in owner.readiness(self.config).values():
            self.assertTrue(state["missingEvidence"])
            self.assertFalse(state["reviewed"])
    def test_nested_corruption_is_rejected_without_crashing(self):
        for client in (None, {}, {"client_info": {"mobilesdk_app_id": 7, "android_client_info": {"package_name": "com.quietcamp.game"}}, "api_key": []}):
            self.config["firebase"]["androidGoogleServices"] = {"project_info": {"project_id": "quiet-camp"}, "client": [client]}
            self.assertTrue(owner.validate(self.config))
    def test_native_output_drops_unknown_owner_fields(self):
        source = {"project_info": {"project_id": "quiet-camp", "project_number": "123456", "not_for_build": "ignored"},
                  "client": [{"client_info": {"mobilesdk_app_id": "1:123456:android:abcdef", "android_client_info": {"package_name": "com.quietcamp.game"}},
                              "api_key": [{"current_key": "AIza" + "a" * 35}], "not_for_build": "ignored"}], "not_for_build": "ignored"}
        self.config["firebase"]["androidGoogleServices"] = source
        self.assertFalse(owner.validate(self.config))
        self.assertNotIn("not_for_build", json.dumps(owner.native_firebase(self.config)))


if __name__ == "__main__": unittest.main()

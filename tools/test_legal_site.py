"""Publication preparation tests, no network and no real publisher data."""
import json
import tempfile
import unittest
from pathlib import Path
import legal_site as site


class LegalSiteTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="qc-legal-test-")
        self.root = Path(self.temp.name)
        self.source = self.root / "reviewed"
        self.source.mkdir()
        self.output = self.root / "pages"
        self.config = {"appName": "QuietCamp", "publisherName": "Test Publisher", "supportEmail": "privacy@quietcamp.dev",
                       "revision": "qa-1", "publishedOn": "2026-10-04", "accountsEnabled": False, "purchasesEnabled": False}
        self.manifest = json.loads(site.MANIFEST.read_text())
        for field, filename in self.manifest["urlFields"].items():
            self.config[field] = "https://quietcamp.dev/legal/" + filename
        for d in self.manifest["documents"]:
            (self.source / d["source"]).write_text("# QuietCamp " + d["id"] + "\n\n{{publisherName}} · {{supportEmail}}\n\nReviewed QA fixture, not a legal policy.\n")

    def tearDown(self):
        self.temp.cleanup()

    def test_release_fails_before_output_for_project_drafts(self):
        config = json.loads(site.LEGAL.read_text())
        with self.assertRaisesRegex(ValueError, "Release preparation blocked"):
            site.generate(config, self.manifest, site.ROOT / "Design/Legal/templates", self.output, True)
        self.assertFalse(self.output.exists())

    def test_preview_has_visible_draft_noindex_and_no_tracker(self):
        config = json.loads(site.LEGAL.read_text())
        report = site.generate(config, self.manifest, site.ROOT / "Design/Legal/templates", self.output)
        self.assertEqual(13, len(report["pages"]))
        self.assertTrue(report["issues"])
        for page in self.output.glob("*.html"):
            text = page.read_text()
            self.assertIn('name="robots" content="noindex,nofollow"', text)
            self.assertIn("DRAFT / ЧЕРНЕТКА / ENTWURF", text)
            self.assertNotIn("<script", text)
            self.assertNotIn("https://fonts.", text)

    def test_reviewed_fixture_prepares_all_links_and_hashes_without_deploying(self):
        report = site.generate(self.config, self.manifest, self.source, self.output, True)
        self.assertFalse(report["deployed"]); self.assertEqual([], report["issues"])
        for entry in report["pages"]:
            text = (self.output / entry["file"]).read_text()
            self.assertIn('lang="' + entry["language"] + '"', text)
            self.assertIn("privacy@quietcamp.dev", text)
            self.assertEqual(64, len(entry["sha256"]))
        self.assertTrue((self.output / ".nojekyll").exists())
        self.assertEqual("User-agent: *\nAllow: /\n", (self.output / "robots.txt").read_text())

    def test_unsafe_html_and_links_cannot_execute(self):
        value = site.markdown('# Hello <script>alert(1)</script>\n\n[bad](javascript:evil)\n\n[ok](https://quietcamp.dev/privacy)')
        self.assertNotIn("<script", value); self.assertNotIn("javascript:", value)
        self.assertIn("&lt;script&gt;", value); self.assertIn('href="https://quietcamp.dev/privacy"', value)
        self.assertIsNone(site.safe_link("https://["))

    def test_stale_contact_blocks_release(self):
        (self.source / "privacy.uk.md").write_text("# QuietCamp\n\nPrevious publisher, no current contact")
        with self.assertRaisesRegex(ValueError, "identity/contact mismatch"):
            site.generate(self.config, self.manifest, self.source, self.output, True)
        self.assertFalse(self.output.exists())

    def test_unsafe_link_blocks_release_instead_of_silently_publishing_it(self):
        path = self.source / "support.en.md"
        path.write_text(path.read_text() + "\n[Click](javascript:evil)")
        with self.assertRaisesRegex(ValueError, "unsafe document link"):
            site.generate(self.config, self.manifest, self.source, self.output, True)

    def test_sources_cannot_escape_directory(self):
        self.manifest["documents"][0]["source"] = "../private.md"
        with self.assertRaisesRegex(ValueError, "inside source directory"):
            site.generate(self.config, self.manifest, self.source, self.output)

    def test_symlink_cannot_read_outside_document_source(self):
        path = self.source / "privacy.uk.md"
        path.unlink()
        outside = self.root / "unrelated.txt"; outside.write_text("not a legal document")
        path.symlink_to(outside)
        with self.assertRaisesRegex(ValueError, "including symlinks"):
            site.generate(self.config, self.manifest, self.source, self.output)

    def test_page_urls_must_match_central_config(self):
        self.config["privacyPolicyUrl"] = "https://quietcamp.dev/stale.html"
        with self.assertRaisesRegex(ValueError, "must point to the generated"):
            site.generate(self.config, self.manifest, self.source, self.output, True)

    def test_existing_pages_are_never_overwritten(self):
        self.output.mkdir(); (self.output / "keep.html").write_text("keep")
        with self.assertRaisesRegex(ValueError, "never overwritten"):
            site.generate(self.config, self.manifest, self.source, self.output, True)
        self.assertEqual("keep", (self.output / "keep.html").read_text())

    def test_missing_language_is_detected(self):
        (self.source / "privacy.de.md").unlink()
        with self.assertRaisesRegex(ValueError, "Missing localized privacy: de"):
            site.generate(self.config, self.manifest, self.source, self.output, True)

    def test_placeholder_pdf_ip_and_invalid_dates_fail(self):
        for endpoint in ["https://example.org/privacy", "https://10.1.2.3/privacy", "https://[::1]/privacy", "http://quietcamp.dev/privacy", "https://quietcamp.dev/privacy.pdf"]:
            self.assertFalse(site.public_url(endpoint), endpoint)
        self.config["publishedOn"] = "2026-02-30"
        with self.assertRaisesRegex(ValueError, "valid yyyy-mm-dd"):
            site.generate(self.config, self.manifest, self.source, self.output, True)


if __name__ == "__main__":
    unittest.main()

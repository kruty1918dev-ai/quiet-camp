#!/usr/bin/env python3
"""Small archive fixtures for containment, metadata and overwrite safety."""
import io
import tarfile
import tempfile
import unittest
from pathlib import Path

from import_unitypackage import asset_path, import_package


class ImportContracts(unittest.TestCase):
    guid = "1" * 32

    def package(self, root, path="Assets/Vendor/model.txt", guid=None, content=b"model"):
        guid = guid or self.guid
        destination = root / "source.unitypackage"
        with tarfile.open(destination, "w:gz") as archive:
            for key, raw in [("pathname", path.encode()), ("asset", content),
                             ("asset.meta", f"fileFormatVersion: 2\nguid: {guid}\n".encode())]:
                info = tarfile.TarInfo(guid + "/" + key)
                info.size = len(raw)
                archive.addfile(info, io.BytesIO(raw))
        return destination

    def test_restore_preserves_metadata_and_second_import_is_identical(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = self.package(root)
            first = import_package(package, root / "project", ["Assets/Vendor"], apply=True)
            self.assertEqual(first["restoredFiles"], 2)
            self.assertEqual((root / "project/Assets/Vendor/model.txt").read_bytes(), b"model")
            second = import_package(package, root / "project", ["Assets/Vendor"], apply=True)
            self.assertEqual(second["identicalFiles"], 2)

    def test_rejects_traversal_and_control_characters(self):
        for path in [b"Assets/../outside", b"/Assets/model", b"Assets/model\nunsafe", b"Assets/C:\\model"]:
            with self.assertRaises(ValueError):
                asset_path(path)
        self.assertEqual(asset_path(b"Assets/Vendor/model.txt\n00"), ("Assets/Vendor/model.txt", True))

    def test_conflict_preflight_leaves_existing_content_untouched(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = self.package(root)
            project = root / "project"
            import_package(package, project, ["Assets/Vendor"], apply=True)
            package = self.package(root, content=b"different")
            with self.assertRaises(ValueError):
                import_package(package, project, ["Assets/Vendor"], apply=True)
            self.assertEqual((project / "Assets/Vendor/model.txt").read_bytes(), b"model")
            # Explicit same-GUID updates are used only for the supplied URP layer.
            import_package(package, project, ["Assets/Vendor"], replace=True, apply=True)
            self.assertEqual((project / "Assets/Vendor/model.txt").read_bytes(), b"different")

    def test_same_guid_at_another_path_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            project = root / "project"
            import_package(self.package(root), project, ["Assets/Vendor"], apply=True)
            with self.assertRaises(ValueError):
                import_package(self.package(root, path="Assets/Vendor/other.txt"), project,
                               ["Assets/Vendor"], apply=True)

    def test_different_guid_at_existing_path_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            project = root / "project"
            import_package(self.package(root), project, ["Assets/Vendor"], apply=True)
            with self.assertRaises(ValueError):
                import_package(self.package(root, guid="2" * 32), project, ["Assets/Vendor"],
                               replace=True, apply=True)

    def test_symlink_archive_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = root / "source.unitypackage"
            with tarfile.open(package, "w:gz") as archive:
                link = tarfile.TarInfo(self.guid + "/asset")
                link.type = tarfile.SYMTYPE
                link.linkname = "/tmp/outside"
                archive.addfile(link)
            with self.assertRaises(ValueError):
                import_package(package, root / "project", ["Assets/Vendor"], apply=True)

    def test_remap_updates_metadata_and_internal_reference_together(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            updated = "2" * 32
            package = self.package(root, path="Assets/Vendor/model.prefab",
                                   content=("guid: " + self.guid + "\n").encode())
            receipt = import_package(package, root / "project", ["Assets/Vendor"], apply=True,
                                     guid_remap={self.guid: updated})
            target = root / "project/Assets/Vendor/model.prefab"
            self.assertIn(updated.encode(), target.read_bytes())
            self.assertIn(updated.encode(), Path(str(target) + ".meta").read_bytes())
            self.assertEqual(receipt["entries"][0]["originalGuid"], self.guid)
            repeated = import_package(package, root / "project", ["Assets/Vendor"], apply=True,
                                      guid_remap={self.guid: updated})
            self.assertEqual(repeated["identicalFiles"], 2)


if __name__ == "__main__":
    unittest.main()

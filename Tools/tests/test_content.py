"""A green player build must contain the exact platform content, not just catalog files."""
import hashlib
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from sectorlab.common import ToolError
from sectorlab.content import verify_content


class ContentTests(unittest.TestCase):
    def setUp(self):
        self.files = {"aa/catalog.bin": b"catalog", "aa/catalog.hash": b"hash",
                      "aa/settings.json": b"{}", "aa/Android/units.bundle": b"UnityFS\0units"}
        self.manifest = {"succeeded": True, "target": "Android", "files": [
            {"path": path, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
            for path, data in self.files.items()]}

    def test_target_build_content_is_verified_byte_for_byte(self):
        self.assertEqual(verify_content(self.manifest, "Android", self.files.__getitem__), self.manifest["files"])

    def test_catalog_only_apk_is_rejected(self):
        del self.files["aa/Android/units.bundle"]
        with self.assertRaisesRegex(ToolError, "missing required local content"):
            verify_content(self.manifest, "Android", self.files.__getitem__)

    def test_stale_bundle_and_stale_catalog_are_rejected(self):
        for name in ("aa/catalog.bin", "aa/Android/units.bundle"):
            with self.subTest(name=name):
                artifact = dict(self.files)
                artifact[name] += b"stale"
                with self.assertRaisesRegex(ToolError, "differs from the target build"):
                    verify_content(self.manifest, "Android", artifact.__getitem__)

    def test_wrong_platform_manifest_is_rejected(self):
        self.manifest["target"] = "StandaloneWindows64"
        with self.assertRaises(ToolError):
            verify_content(self.manifest, "Android", self.files.__getitem__)

    def test_another_platforms_bundle_cannot_enter_android(self):
        self.manifest["files"][-1]["path"] = "aa/StandaloneWindows64/units.bundle"
        with self.assertRaisesRegex(ToolError, "another platform"):
            verify_content(self.manifest, "Android", self.files.__getitem__)

    def test_content_manifest_cannot_read_outside_the_player(self):
        self.manifest["files"].append({"path": "aa/../../private", "bytes": 1, "sha256": "a" * 64})
        with self.assertRaisesRegex(ToolError, "Invalid relative path"):
            verify_content(self.manifest, "Android", self.files.__getitem__)


if __name__ == "__main__":
    unittest.main()

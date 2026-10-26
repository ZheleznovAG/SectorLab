"""Evidence must remain readable and portable on the minimum supported Python."""
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from sectorlab import common


class EvidenceTests(unittest.TestCase):
    def test_unicode_record_uses_utf8_and_lf(self):
        record = {"task": "проверка", "sourceHead": "a" * 40, "passed": 76}
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(common, "EVIDENCE", Path(directory) / "evidence"):
                path = common.write_evidence("test.json", record)
                raw = path.read_bytes()
        self.assertEqual(json.loads(raw.decode("utf-8")), record)
        self.assertTrue(raw.endswith(b"\n"))
        self.assertNotIn(b"\r", raw)


if __name__ == "__main__":
    unittest.main()

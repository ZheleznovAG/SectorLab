"""Evidence must remain readable and portable on the minimum supported Python."""
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from sectorlab import android
from sectorlab import common
from sectorlab import unity
import sl


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

    def test_existing_evidence_is_never_replaced(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(common, "EVIDENCE", Path(directory)):
                path = common.write_evidence("test.json", {"result": "original"})
                original = path.read_bytes()
                with self.assertRaises(common.ToolError):
                    common.write_evidence("test.json", {"result": "replacement"})
                self.assertEqual(path.read_bytes(), original)

    def test_existing_report_or_csv_prevents_unity_test_start(self):
        for filename in ("tests-run-playmode.json", "run-main-restarts.csv"):
            with self.subTest(filename=filename), tempfile.TemporaryDirectory() as directory:
                evidence = Path(directory)
                path = evidence / filename
                path.write_bytes(b"previous result")
                with patch.object(unity, "EVIDENCE", evidence), patch.object(unity, "command") as command:
                    with self.assertRaises(common.ToolError):
                        unity.run_tests("PlayMode", "run")
                    command.assert_not_called()
                self.assertEqual(path.read_bytes(), b"previous result")

    def test_both_modes_are_preflighted_before_running_either(self):
        with tempfile.TemporaryDirectory() as directory:
            evidence = Path(directory)
            (evidence / "run-main-restarts.csv").write_bytes(b"original")
            with patch.object(unity, "EVIDENCE", evidence), patch.object(unity, "run_tests") as run:
                with patch("sys.stderr"):
                    self.assertEqual(sl.main(["test", "--task", "run", "--mode", "both"]), 1)
                run.assert_not_called()

    def test_dotted_measurement_task_keeps_its_full_name(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(android, "EVIDENCE", Path(directory)):
                names = [path.name for path in android.measurement_paths("v1.3.1-phone")]
        self.assertEqual(names, ["v1.3.1-phone.json", "v1.3.1-phone.csv", "v1.3.1-phone-frames.csv"])

    def test_existing_measurement_csv_prevents_a_new_measurement(self):
        with tempfile.TemporaryDirectory() as directory:
            evidence = Path(directory)
            path = evidence / "v1.3.1-phone.csv"
            path.write_bytes(b"previous samples")
            with patch.object(android, "EVIDENCE", evidence), patch.object(android, "find_adb") as adb:
                with self.assertRaises(common.ToolError):
                    android.measure("v1.3.1-phone", 30, None, None, None, False)
                adb.assert_not_called()
            self.assertEqual(path.read_bytes(), b"previous samples")


if __name__ == "__main__":
    unittest.main()

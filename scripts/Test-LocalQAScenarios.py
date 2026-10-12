"""CPU-only full-scenario preparation regression; no model, game, or installed profile calls."""
import importlib.util
import json
import math
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("scenarios", Path(__file__).with_name("Prepare-LocalQAScenarios.py"))
scenarios = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scenarios)


class ScenarioTests(unittest.TestCase):
    def test_complete_suite_and_files(self):
        suite = scenarios.load(scenarios.ROOT, scenarios.ROOT / "specs/qa/scenarios/full-suite.json")
        self.assertEqual(len(suite["scenarios"]), 12)
        for item in suite["scenarios"]:
            self.assertGreaterEqual(len(item["milestones"]), 4)
            self.assertIn("full_scenario_executor", item["required_capabilities"])
        for relative in suite["fixtures"].values():
            if not relative.startswith("artifacts/"):
                self.assertTrue(scenarios.source_path(scenarios.ROOT, relative).is_file(), relative)
        heavy = scenarios.source_path(scenarios.ROOT, suite["fixtures"]["heavy_supported"]).read_text(encoding="utf-8")
        parts = [line.split(";") for line in heavy.splitlines() if line and not line.startswith("#")]
        self.assertEqual(len(parts), 1024)
        self.assertEqual({p[0] for p in parts}, {"wood_pole", "wood_floor"})
        for part in parts:
            self.assertTrue(all(math.isfinite(float(v)) for v in part[2:9] + part[10:13]))
            self.assertTrue(all(.01 <= float(v) <= 10 for v in part[10:13]))
            self.assertLess(abs(float(part[2])), 48)
            self.assertLess(abs(float(part[4])), 48)
        with self.assertRaises(json.JSONDecodeError):
            json.loads(scenarios.source_path(scenarios.ROOT, suite["fixtures"]["invalid_json"]).read_text(encoding="utf-8"))

    def test_paths_reject_escape_and_absolute(self):
        for relative in ("../other/world.db", "C:/world.db", "//server/share/file", "tests\\fixture.json"):
            with self.subTest(relative=relative), self.assertRaises(ValueError):
                scenarios.source_path(scenarios.ROOT, relative)

    def test_offline_bundle_and_policy_failures(self):
        original = scenarios.load(scenarios.ROOT, scenarios.ROOT / "specs/qa/scenarios/full-suite.json")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            suite = json.loads(json.dumps(original))
            suite["fixtures"] = {"test": "test.json", "optional_external": "artifacts/private/no-copy.blueprint"}
            for scenario in suite["scenarios"]:
                scenario["fixtures"] = ["test", "optional_external"]
            (root / "test.json").write_text("{}", encoding="utf-8")
            manifest = root / "suite.json"
            manifest.write_text(json.dumps(suite), encoding="utf-8")
            with patch.object(scenarios.agent.probe, "api", side_effect=AssertionError("No model calls")), patch.object(scenarios.agent, "invoke", side_effect=AssertionError("No game calls")):
                output, report = scenarios.prepare(root, manifest)
            self.assertFalse(report["game_tests_started"])
            self.assertFalse(report["model_started"])
            self.assertEqual(len(list(output.glob("S*.task.json"))), 12)
            self.assertTrue(all(s["readiness"] == "blocked" and s["status"] == "prepared" for s in report["scenarios"]))
            self.assertTrue(all(s["missing_files"] == ["optional_external"] for s in report["scenarios"]))
            packet = json.loads((output / "S01.task.json").read_text(encoding="utf-8"))
            self.assertEqual(packet["task_kind"], "buildworks_full_scenario")
            self.assertEqual(packet["fixtures"]["test"]["sha256"], "44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a")
            event = json.loads((output / "events.jsonl").read_text(encoding="utf-8"))
            self.assertEqual(event["kind"], "offline_tasks_prepared")
            for fault in ("missing_gate", "duplicate_id", "cycle", "no_evidence", "empty_objective", "live"):
                bad = json.loads(json.dumps(suite))
                if fault == "missing_gate": bad["scenarios"][0]["required_capabilities"] = ["native_frame"]
                if fault == "duplicate_id": bad["scenarios"][1]["id"] = "S01"
                if fault == "cycle": bad["scenarios"][0]["depends_on"] = ["S01"]
                if fault == "no_evidence": bad["scenarios"][0]["milestones"][0]["evidence"] = []
                if fault == "empty_objective": bad["scenarios"][0]["objective"] = ""
                if fault == "live": bad["execution"] = "run_now"
                manifest.write_text(json.dumps(bad), encoding="utf-8")
                with self.subTest(fault=fault), self.assertRaises(ValueError):
                    scenarios.load(root, manifest)


if __name__ == "__main__":
    unittest.main()

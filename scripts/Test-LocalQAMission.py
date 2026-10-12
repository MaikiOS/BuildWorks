"""CPU-only policy and full local-mission loop regressions; no model/game calls."""
from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("mission", Path(__file__).with_name("Run-LocalQAMission.py"))
mission = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mission)
TASK = mission.agent.read_json(mission.agent.ROOT / "specs/qa/tasks/inspect-yard.json")


def decision(action, **args):
    return json.dumps({"action": action, "args": args, "reason": "Observed task evidence"})


def state():
    return {"id": 1, "status": "completed", "nonce": "a" * 32, "world": "BW_QA_aaaaaaaa",
            "ready": True, "focused": True, "stopRevision": 7, "inventoryVisible": False,
            "inventory": "frame=100 stacks=1\n0:Wood#123 count=30", "selectedUi": "",
            "uiModule": "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
            "position": {"x": 2560, "y": 41.09939, "z": 128},
            "cameraEuler": {"x": 20, "y": 179, "z": 0}, "screenshot": "screenshot.png"}


def ui_task():
    rule = {"scope": "BuildWorksExchange", "name": "ExchangeDirectory", "kind": "TMP_InputField",
            "actions": ["click", "text"], "texts": [{"value": "QA approved path", "submit": False}]}
    return dict(TASK, id="ui-contract-test", allowed_actions=["click", "text", "finish", "stop"], allowed_keys=[],
                ui_policy=[rule], success={"inventory_unchanged": True, "ui_values": [
                    {k: rule[k] for k in ("scope", "name", "kind")} | {"value": "QA approved path"}]})


def ui_state():
    return dict(state(), uiTargets=[{"id": "c" * 32, "scope": "BuildWorksExchange", "name": "ExchangeDirectory",
                                    "kind": "TMP_InputField", "label": "", "value": "initial",
                                    "writable": True, "focused": False, "valueTruncated": False}], uiTargetsTruncated=False)


class MissionTests(unittest.TestCase):
    def test_ui_policy_steps_criteria_and_boundaries(self):
        task, observed = ui_task(), ui_state()
        mission.task_contract(task)
        _, steps = mission.action_contract(decision("click", target="c" * 32), task, observed)
        self.assertEqual(steps, [{"milliseconds": 100, "uiTarget": "c" * 32, "pointer": True, "buttons": 1}, {"milliseconds": 300}])
        with self.assertRaises(ValueError):
            mission.action_contract(decision("text", target="c" * 32, value="QA approved path", submit=False), task, observed)
        observed["uiTargets"][0]["focused"] = True
        _, steps = mission.action_contract(decision("text", target="c" * 32, value="QA approved path", submit=False), task, observed)
        self.assertEqual(steps[0], {"milliseconds": 100, "uiTarget": "c" * 32, "text": "QA approved path"})
        self.assertFalse(mission.criteria(task, [observed])["ui_values"]["passed"])
        observed["uiTargets"][0]["value"] = "QA approved path"
        self.assertTrue(mission.criteria(task, [observed])["ui_values"]["passed"])
        observed["uiTargets"][0]["valueTruncated"] = True
        self.assertFalse(mission.criteria(task, [observed])["ui_values"]["passed"])
        observed["uiTargets"][0]["valueTruncated"] = False
        observed["uiTargetsTruncated"] = True
        self.assertFalse(mission.criteria(task, [observed])["ui_values"]["passed"])
        observed["uiTargetsTruncated"] = False
        duplicate_selector = dict(observed, uiTargets=observed["uiTargets"] + [dict(observed["uiTargets"][0], id="d" * 32, value="different")])
        self.assertFalse(mission.criteria(task, [duplicate_selector])["ui_values"]["passed"])
        for flag in (None, 0, "false"):
            bad = dict(observed, uiTargetsTruncated=flag)
            with self.subTest(flag=flag), self.assertRaises(ValueError): mission.ui_entries(bad)
        bad = dict(observed); del bad["uiTargetsTruncated"]
        with self.assertRaises(ValueError): mission.ui_entries(bad)
        for action in (decision("click", target="d" * 32), decision("click", target="c" * 32, x=.5),
                       decision("text", target="c" * 32, value="spawn SwordIron", submit=False),
                       decision("text", target="c" * 32, value="QA approved path", submit=True),
                       decision("text", target="c" * 32, value="QA approved path", submit=1)):
            with self.subTest(action=action), self.assertRaises(ValueError):
                mission.action_contract(action, task, observed)
        for key, value in (("scope", "Console"), ("name", "Different"), ("kind", "Button"), ("writable", False), ("focused", False)):
            bad = json.loads(json.dumps(observed)); bad["uiTargets"][0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                mission.action_contract(decision("text", target="c" * 32, value="QA approved path", submit=False), task, bad)
        duplicate = dict(observed, uiTargets=observed["uiTargets"] * 2)
        with self.assertRaises(ValueError): mission.ui_target(duplicate, "c" * 32)
        missing = state()
        with self.assertRaises(ValueError): mission.ui_target(missing, "c" * 32)
        for mutation in ("no_policy", "console", "duplicate", "unused_action", "overlong", "bool_submit", "wrong_kind", "wildcard", "criterion"):
            bad = json.loads(json.dumps(task))
            if mutation == "no_policy": del bad["ui_policy"]
            if mutation == "console": bad["ui_policy"][0]["scope"] = "Console"
            if mutation == "duplicate": bad["ui_policy"] *= 2
            if mutation == "unused_action": bad["ui_policy"][0]["actions"] = ["text"]
            if mutation == "overlong": bad["ui_policy"][0]["texts"][0]["value"] = "x" * 257
            if mutation == "bool_submit": bad["ui_policy"][0]["texts"][0]["submit"] = 1
            if mutation == "wrong_kind": bad["ui_policy"][0]["kind"] = "Button"
            if mutation == "wildcard": bad["ui_policy"][0]["name"] = "*"
            if mutation == "criterion": bad["success"]["ui_values"][0]["value"] = "x" * 129
            with self.subTest(mutation=mutation), self.assertRaises(ValueError): mission.task_contract(bad)
        branches = {b["properties"]["action"]["enum"][0]: b for b in mission.response_schema(task)["oneOf"]}
        self.assertEqual(branches["text"]["properties"]["args"]["properties"]["value"]["enum"], ["QA approved path"])
        submitted = json.loads(json.dumps(task)); submitted["ui_policy"][0]["texts"][0]["submit"] = True
        _, steps = mission.action_contract(decision("text", target="c" * 32, value="QA approved path", submit=True), submitted, observed)
        self.assertEqual(steps[0]["keys"], ["Return"])

    def test_ui_whole_loop_never_remaps_or_replays(self):
        for fault in (None, "stale", "unseen", "premature_finish", "native_timeout", "readonly"):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary) / "Default/BuildWorks"; root.mkdir(parents=True)
                task_path = root / "task.json"; task_path.write_text(json.dumps(ui_task()), encoding="utf-8")
                control = root.parent.parent / "TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/qa-saves" / ("a" * 32) / "qa-control"
                control.mkdir(parents=True)
                (control.parent.parent.parent / "qa-isolation.token").write_text("a" * 32, encoding="utf-8")
                (control / "session.json").write_text(json.dumps({"version": 1, "nonce": "a" * 32}), encoding="utf-8")
                current, calls = ui_state(), []
                commands = [decision("click", target="c" * 32), decision("text", target="c" * 32, value="QA approved path", submit=False), decision("finish")]
                if fault == "premature_finish": commands = [decision("finish")]
                if fault == "unseen": current["uiTargets"] = [] # The same ID appearing only after inference is not authorized.
                def choose(*_):
                    if fault == "stale": current["uiTargets"][0]["id"] = "d" * 32
                    if fault == "unseen": current["uiTargets"] = ui_state()["uiTargets"]
                    if fault == "readonly" and current["uiTargets"][0]["focused"]: current["uiTargets"][0]["writable"] = False
                    return {"done": True, "done_reason": "stop", "response": commands.pop(0)}
                def invoke(action, nonce, revision=None, capture=False, stop=False, steps=None):
                    calls.append(("stop" if stop else action, steps))
                    if stop: return {"stop_requested": True}
                    if action == "batch":
                        if "text" in steps[0]:
                            if fault == "native_timeout": raise TimeoutError("Unknown UI text outcome")
                            current["uiTargets"][0]["value"] = steps[0]["text"]
                        else:
                            current["uiTargets"][0]["focused"] = True; current["selectedUi"] = "ExchangeDirectory"
                    current["id"] += 1
                    return json.loads(json.dumps(current))
                with patch.object(mission.agent, "ROOT", root), patch.object(mission.agent, "invoke", side_effect=invoke), patch.object(mission, "choose", side_effect=choose), patch.object(mission.agent, "screenshot", return_value=b"PNG"), patch.object(mission.agent.probe, "api", return_value={"models": [{"name": "test"}]}), redirect_stdout(io.StringIO()):
                    code = mission.run(task_path, "test")
                report = json.loads(next((root / "artifacts/local-qa-agent").glob("*-mission-*/report.json")).read_text(encoding="utf-8"))
                batches = [s for action, s in calls if action == "batch"]
                self.assertEqual(code, 0 if fault is None else 1, report.get("error"))
                self.assertEqual(len(batches), 2 if fault in (None, "native_timeout") else 1 if fault == "readonly" else 0)
                self.assertEqual(report["status"], "completed" if fault is None else "unverified" if fault == "premature_finish" else "failed")
                events = [json.loads(line) for line in next((root / "artifacts/local-qa-agent").glob("*-mission-*/events.jsonl")).read_text(encoding="utf-8").splitlines()]
                self.assertTrue(any(e["kind"] == "model" for e in events))
                dispatched = [e for e in events if e["kind"] == "dispatch"]
                self.assertEqual(len(dispatched), len(batches))
                if fault == "native_timeout":
                    self.assertEqual(dispatched[-1]["value"]["steps"][0]["text"], "QA approved path")
                    self.assertEqual(sum(e["kind"] == "native_action" for e in events), 1)
                self.assertFalse((root / "artifacts/local-qa-agent/agent.lock").exists())
                if fault is None:
                    self.assertTrue(report["criteria"]["ui_values"]["passed"])
                    self.assertEqual(batches[1][0]["text"], "QA approved path")

    def test_policy_boundary(self):
        mission.task_contract(TASK)
        for changes in ({"version": True}, {"extra": 1}, {"success": {"house_exists": True}},
                        {"limits": {"max_steps": 1000, "max_seconds": 180, "max_distance": 1}},
                        {"allowed_actions": ["execute_shell"]}, {"allowed_keys": ["F5"]},
                        {"allowed_actions": ["click"]}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                mission.task_contract(dict(TASK, **changes))
        for raw in (decision("press", key="F5"), decision("move", key="W", milliseconds=200),
                    decision("click", x=.5, y=.5, button=1), decision("text", value="spawn SwordIron"),
                    decision("look", yaw_degrees=float("nan"), pitch_degrees=0),
                    decision("look", yaw_degrees=31, pitch_degrees=0),
                    decision("look", x=2561.009033203125, y=44.90020751953125, milliseconds=500),
                    decision("finish", extra=True), '{"action":"finish","action":"stop","args":{},"reason":"x"}'):
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                mission.action_contract(raw, TASK, state())

    def test_exact_bridge_steps_and_ui_policy(self):
        branches = {b["properties"]["action"]["enum"][0]: b for b in mission.response_schema(TASK)["oneOf"]}
        self.assertEqual(set(branches), set(TASK["allowed_actions"]))
        self.assertEqual(set(branches["look"]["properties"]["args"]["required"]), {"yaw_degrees", "pitch_degrees"})
        self.assertNotIn("key", branches["look"]["properties"]["args"]["properties"])
        self.assertEqual(branches["press"]["properties"]["args"]["properties"]["key"]["enum"], TASK["allowed_keys"])
        with self.assertRaisesRegex(ValueError, "Unknown/missing action arguments"):
            mission.action_contract(decision("look", key="W", milliseconds=500), TASK, state())
        value, steps = mission.action_contract(decision("look", yaw_degrees=20, pitch_degrees=0), TASK, state())
        self.assertEqual(steps, [{"milliseconds": 500, "lookX": 800, "lookY": 0}, {"milliseconds": 300}])
        with patch.object(subprocess, "run", return_value=subprocess.CompletedProcess([], 0, '{}', '')) as call:
            mission.agent.invoke("batch", "a" * 32, 7, steps=steps)
            args = call.call_args.args[0]
            self.assertEqual(json.loads(args[args.index("-StepsJson") + 1]), steps)
        extended = dict(TASK, allowed_actions=["move"], allowed_keys=[])
        mission.task_contract(extended)
        with self.assertRaises(ValueError):
            mission.action_contract(decision("move", key="W", milliseconds=200), extended, state())
        _, steps = mission.action_contract(decision("move", key="W", milliseconds=200), dict(extended, allowed_keys=["W"]), state())
        self.assertEqual(steps[0]["keys"], ["W"])

    def test_native_criteria_not_model_claim(self):
        base = state()
        opened = dict(base, id=2, inventoryVisible=True)
        final = dict(base, id=3, cameraEuler={"x": 20, "y": 199, "z": 0})
        self.assertFalse(all(v["passed"] for v in mission.criteria(TASK, [base]).values()))
        self.assertTrue(all(v["passed"] for v in mission.criteria(TASK, [base, opened, final]).values()))
        changed = dict(final, inventory="frame=102 stacks=1\n0:Wood#123 count=31")
        self.assertFalse(mission.criteria(TASK, [base, opened, changed])["inventory_unchanged"]["passed"])
        wrapped = dict(base, cameraEuler={"x": 20, "y": 359, "z": 0})
        final["cameraEuler"]["y"] = 19
        self.assertTrue(mission.criteria(TASK, [wrapped, opened, final])["yaw_change"]["passed"])

    def test_whole_mission_decisions_and_abort_without_replay(self):
        for fault in (None, "look_first", "premature_finish", "focus", "stop_revision", "camera", "native_timeout", "inventory_change", "locked"):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary) / "Default/BuildWorks"
                root.mkdir(parents=True)
                task_path = root / "task.json"
                task_path.write_text(json.dumps(TASK), encoding="utf-8")
                control = root.parent.parent / "TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/qa-saves" / ("a" * 32) / "qa-control"
                control.mkdir(parents=True)
                (control.parent.parent.parent / "qa-isolation.token").write_text("a" * 32, encoding="utf-8")
                (control / "session.json").write_text(json.dumps({"version": 1, "nonce": "a" * 32}), encoding="utf-8")
                current = state()
                calls = []
                commands = ([decision("look", yaw_degrees=20, pitch_degrees=0), decision("press", key="Tab"), decision("press", key="Tab")] if fault == "look_first" else
                            [decision("press", key="Tab"), decision("press", key="Tab"), decision("look", yaw_degrees=20, pitch_degrees=0)])
                commands += [decision("finish")]
                if fault == "premature_finish":
                    commands = [decision("finish")]

                def choose(*_):
                    if fault == "focus": current["focused"] = False
                    if fault == "stop_revision": current["stopRevision"] += 1
                    if fault == "camera": current["cameraEuler"]["y"] += 5
                    return {"done": True, "done_reason": "stop", "response": commands.pop(0)}

                def invoke(action, nonce, revision=None, capture=False, stop=False, steps=None):
                    calls.append("stop" if stop else action)
                    if stop: return {"stop_requested": True}
                    if action == "batch":
                        if fault == "native_timeout": raise TimeoutError("Native outcome unknown")
                        if fault == "inventory_change": current["inventory"] = "changed"
                        if steps[0].get("keys") == ["Tab"]: current["inventoryVisible"] = not current["inventoryVisible"]
                        if "lookX" in steps[0]: current["cameraEuler"]["y"] += 20
                    current["id"] += 1
                    return json.loads(json.dumps(current))

                lock = root / "artifacts/local-qa-agent/agent.lock"
                if fault == "locked":
                    lock.parent.mkdir(parents=True)
                    lock.write_text("OTHER OWNER", encoding="utf-8")
                with patch.object(mission.agent, "ROOT", root), patch.object(mission.agent, "invoke", side_effect=invoke), patch.object(mission, "choose", side_effect=choose), patch.object(mission.agent, "screenshot", return_value=b"PNG"), patch.object(mission.agent.probe, "api", return_value={"models": [{"name": "test"}]}), redirect_stdout(io.StringIO()):
                    code = mission.run(task_path, "test")
                report = json.loads(next((root / "artifacts/local-qa-agent").glob("*-mission-*/report.json")).read_text(encoding="utf-8"))
                self.assertEqual(code, 0 if fault in (None, "look_first") else 1, report.get("error"))
                self.assertEqual(calls.count("batch"), 3 if fault in (None, "look_first") else 1 if fault in ("native_timeout", "inventory_change") else 0, report.get("error"))
                self.assertEqual(calls.count("stop"), 0 if fault in (None, "look_first", "locked") else 1)
                self.assertEqual(report["status"], "completed" if fault in (None, "look_first") else "unverified" if fault == "premature_finish" else "failed")
                self.assertEqual(lock.exists(), fault == "locked")


if __name__ == "__main__":
    unittest.main()

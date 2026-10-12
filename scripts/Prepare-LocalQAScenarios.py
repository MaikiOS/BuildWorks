"""Prepare complete local-agent task packets offline. Never call the model or game."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import time
import uuid

spec = importlib.util.spec_from_file_location("qa_agent", Path(__file__).with_name("Run-LocalQAAgent.py"))
agent = importlib.util.module_from_spec(spec)
spec.loader.exec_module(agent)
ROOT = agent.ROOT
# These are source contracts, not a new live-session readiness observation.
CURRENT_CAPABILITIES = {"native_frame", "native_pose", "inventory_summary", "native_snap_summary",
                        "bounded_press", "bounded_look", "bounded_move", "append_event_log"}


def strings(values):
    return isinstance(values, list) and 0 < len(values) <= 40 and all(type(v) is str and 0 < len(v) <= 4000 for v in values)


def source_path(root, relative):
    if type(relative) is not str or not relative or "\\" in relative or ":" in relative or Path(relative).is_absolute():
        raise ValueError("Fixture must use a project-relative path")
    candidate = root / relative
    agent.checked_path(candidate)
    if root.resolve() not in candidate.resolve().parents:
        raise ValueError("Fixture escapes project")
    return candidate


def load(root, manifest):
    value = json.loads(agent.read_bytes(manifest, 512 * 1024), object_pairs_hook=agent.probe.unique_object)
    expected = {"version", "id", "title", "execution", "policy", "logging", "fixtures", "scenarios"}
    if (type(value) is not dict or set(value) != expected or type(value["version"]) is not int or value["version"] != 1 or
            value["execution"] != "paused_until_owner_authorizes_game_tests" or
            not strings([value["id"], value["title"]]) or not strings(value["policy"])):
        raise ValueError("Invalid full-scenario manifest")
    logging = value["logging"]
    if (type(logging) is not dict or set(logging) != {"events", "status", "report", "screenshots", "checkpoint", "event_fields", "statuses", "rules"} or
            any(not strings(logging[k]) for k in ("event_fields", "statuses", "rules")) or
            any(type(logging[k]) is not str or not logging[k] for k in ("events", "status", "report", "screenshots", "checkpoint"))):
        raise ValueError("Invalid logging contract")
    fixtures = value["fixtures"]
    if type(fixtures) is not dict or not 1 <= len(fixtures) <= 100:
        raise ValueError("Invalid fixture map")
    for key, relative in fixtures.items():
        if not re.fullmatch("[a-z][a-z0-9_]{0,63}", key):
            raise ValueError("Invalid fixture ID")
        source_path(root, relative)
    scenarios, seen = value["scenarios"], set()
    if not isinstance(scenarios, list) or not 1 <= len(scenarios) <= 30:
        raise ValueError("Invalid scenario list")
    for scenario in scenarios:
        keys = {"id", "title", "objective", "depends_on", "fixtures", "required_capabilities", "limits", "setup", "milestones"}
        if type(scenario) is not dict or set(scenario) != keys or not strings([scenario["title"], scenario["objective"]]):
            raise ValueError("Invalid scenario")
        sid = scenario["id"]
        if type(sid) is not str or not re.fullmatch("S[0-9]{2}", sid) or sid in seen:
            raise ValueError("Invalid/duplicate scenario ID")
        if not isinstance(scenario["depends_on"], list) or any(type(dep) is not str or dep not in seen for dep in scenario["depends_on"]):
            raise ValueError("Unknown/forward/cyclic scenario dependency")
        if not strings(scenario["fixtures"]) or any(f not in fixtures for f in scenario["fixtures"]):
            raise ValueError("Unknown scenario fixture")
        if (not strings(scenario["required_capabilities"]) or "full_scenario_executor" not in scenario["required_capabilities"] or
                not strings(scenario["setup"])):
            raise ValueError("Missing full-execution gate/setup")
        limits = scenario["limits"]
        if (type(limits) is not dict or set(limits) != {"seconds", "actions"} or
                any(type(v) is not int for v in limits.values()) or not 60 <= limits["seconds"] <= 3600 or not 10 <= limits["actions"] <= 1000):
            raise ValueError("Invalid scenario limits")
        milestones, ids = scenario["milestones"], set()
        if not isinstance(milestones, list) or not 3 <= len(milestones) <= 20:
            raise ValueError("A full scenario needs at least three evidence milestones")
        for milestone in milestones:
            if (type(milestone) is not dict or set(milestone) != {"id", "task", "expect", "evidence"} or
                    not strings([milestone["id"], milestone["task"], milestone["expect"]]) or not strings(milestone["evidence"])):
                raise ValueError("Invalid evidence milestone")
            if milestone["id"] in ids:
                raise ValueError("Duplicate milestone ID")
            ids.add(milestone["id"])
        seen.add(sid)
    return value


def render(packet):
    scenario = packet["scenario"]
    lines = ["# " + scenario["id"] + " — " + scenario["title"], "", scenario["objective"], "",
             "**Подготовлено офлайн. Игровое выполнение заблокировано до разрешения и готовности исполнителя.**", "",
             "## Условия", ""]
    lines += ["- " + v for v in packet["policy"] + scenario["setup"]]
    lines += ["", "## Файлы", "", "| ID | Файл | SHA-256 |", "| --- | --- | --- |"]
    lines += ["| " + key + " | `" + info["path"] + "` | " + info.get("sha256", "НЕТ ФАЙЛА") + " |" for key, info in packet["fixtures"].items()]
    lines += ["", "## Полное задание", ""]
    for milestone in scenario["milestones"]:
        lines += ["### " + milestone["id"], "", milestone["task"], "", "Ожидается: " + milestone["expect"], "",
                  "Доказательства: " + ", ".join(milestone["evidence"]) + ".", ""]
    lines += ["## Блокеры исполнителя", ""] + ["- " + v for v in packet["missing_capabilities"]]
    lines += ["", "## Журнал", "", "`events.jsonl` / `status.json` / `report.json`, кадры `frames/<event_id>.png`.", ""]
    lines += ["- " + v for v in packet["logging"]["rules"]]
    return "\n".join(lines) + "\n"


def prepare(root, manifest):
    value = load(root, manifest)
    snapshots = {}
    for key, relative in value["fixtures"].items():
        path = source_path(root, relative)
        info = {"path": relative, "exists": path.is_file()}
        if info["exists"]:
            if path.stat().st_size > 32 * 1024 * 1024:
                raise ValueError("Fixture exceeds offline preparation budget: " + relative)
            info.update(sha256=hashlib.sha256(agent.read_bytes(path, 32 * 1024 * 1024)).hexdigest(), bytes=path.stat().st_size)
        snapshots[key] = info
    output = root / "artifacts/local-qa-plans" / (time.strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:8])
    agent.checked_path(output)
    output.mkdir(parents=True, exist_ok=False)
    summary = {"bundle": value["id"], "prepared_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
               "manifest_sha256": hashlib.sha256(agent.read_bytes(manifest, 512 * 1024)).hexdigest(),
               "game_tests_started": False, "model_started": False, "scenarios": []}
    for scenario in value["scenarios"]:
        missing = sorted(set(scenario["required_capabilities"]) - CURRENT_CAPABILITIES)
        packet = {"version": 1, "task_kind": "buildworks_full_scenario", "execution": value["execution"],
                  "scenario": scenario, "policy": value["policy"], "logging": value["logging"],
                  "fixtures": {key: snapshots[key] for key in scenario["fixtures"]}, "missing_capabilities": missing,
                  "completion_rule": "All milestones require actual evidence; missing evidence never counts as completed."}
        name = scenario["id"]
        (output / (name + ".task.json")).write_text(json.dumps(packet, ensure_ascii=False, indent=2), encoding="utf-8")
        (output / (name + ".md")).write_text(render(packet), encoding="utf-8")
        summary["scenarios"].append({"id": name, "title": scenario["title"], "status": "prepared", "readiness": "blocked",
                                      "missing_capabilities": missing, "missing_files": [k for k, v in packet["fixtures"].items() if not v["exists"]],
                                      "task_file": name + ".task.json", "milestones": len(scenario["milestones"])})
    (output / "preflight.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    (output / "events.jsonl").write_text(json.dumps({"utc": summary["prepared_utc"], "kind": "offline_tasks_prepared",
                                                   "game_tests_started": False, "model_started": False, "scenarios": len(summary["scenarios"])}, ensure_ascii=False) + "\n", encoding="utf-8")
    index = ["# Полные задания локальной модели", "", "Подготовлено офлайн. Игра и модель не запускались.", "",
             "| Сценарий | Задание | Контрольных точек | Готовность запуска |", "| --- | --- | --- | --- |"]
    index += ["| " + s["id"] + " | [" + s["title"] + "](" + s["id"] + ".md) | " + str(s["milestones"]) + " | Заблокирован: нужен полный исполнитель |" for s in summary["scenarios"]]
    index += ["", "`preflight.json` содержит точные недостающие возможности/файлы. Это не журнал выполненных игровых проверок.",
              "", "Не передавать эти задачи старому пилоту как inventory/camera: его узкие критерии не докажут полный результат.", ""]
    (output / "README.md").write_text("\n".join(index), encoding="utf-8")
    return output, summary


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    directory, result = prepare(ROOT, ROOT / "specs/qa/scenarios/full-suite.json")
    print(json.dumps({"output": str(directory), "scenarios": len(result["scenarios"]),
                      "missing_files": sorted({f for s in result["scenarios"] for f in s["missing_files"]}),
                      "game_tests_started": False, "model_started": False}, ensure_ascii=False))

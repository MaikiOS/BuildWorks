"""One isolated GPU comparison, then restore and compile the ordinary editor."""
import hashlib
import json
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts/checkpoint-247"
OUT.mkdir(parents=True, exist_ok=True)
PROJECT = ROOT / "tools/BuildWorks.UiWorkbench"
UNITY = Path(r"C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe")
NVIDIA = Path(r"C:\Windows\system32\nvidia-smi.exe")

def processes():
    result = subprocess.run(["pwsh.exe", "-NoLogo", "-NoProfile", "-Command",
                             "@(Get-Process -Name valheim,valheim_server,Unity -ErrorAction SilentlyContinue).Count"],
                            capture_output=True, text=True, check=True)
    return int(result.stdout.strip())

def memory():
    result = subprocess.run([str(NVIDIA), "--query-gpu=memory.used,memory.free,utilization.gpu",
                             "--format=csv,noheader,nounits"], capture_output=True, text=True, check=True)
    used, free, utilization = map(int, result.stdout.strip().split(","))
    return dict(usedMiB=used, freeMiB=free, utilizationPercent=utilization)

def launch(method, name):
    log = OUT / (name + "-unity.log")
    started = time.time()
    command = [str(UNITY), "-batchmode", "-force-d3d11", "-projectPath", str(PROJECT),
               "-executeMethod", method, "-logFile", str(log), "-quit"]
    process = subprocess.Popen(command, creationflags=subprocess.CREATE_NO_WINDOW)
    row = dict(pid=process.pid, command=command, memorySamples=[])
    try:
        while process.poll() is None:
            row["memorySamples"].append(dict(seconds=round(time.time() - started, 3), **memory()))
            if time.time() - started > 300:
                process.kill()
                process.wait()
                raise RuntimeError("Own isolated Unity exceeded300s; only its process was stopped.")
            time.sleep(1)
        row["exitCode"] = process.returncode
        row["seconds"] = time.time() - started
        print(name, "exit", process.returncode, flush=True)
        if process.returncode:
            raise RuntimeError("Unity failed; see " + str(log))
        proof = OUT / ("gpu-oracle.json" if name == "candidate" else "editor-baseline.json")
        assert proof.is_file() and proof.stat().st_mtime >= started, "Fresh Unity proof missing."
    finally:
        (OUT / (name + "-process.json")).write_text(json.dumps(row, indent=2), encoding="utf-8")

assert processes() == 0, "Do not interrupt a player/editor session."
preflight = memory()
# Sequential pools + three dense references fit <1GiB of counted fog buffers; leave >=1GiB extra for assets/staging.
assert preflight["freeMiB"] >= 2048, "Insufficient measured GPU headroom for this isolated comparison."
(OUT / "gpu-preflight.json").write_text(json.dumps(preflight, indent=2), encoding="utf-8")
assert UNITY.is_file()
frozen = (PROJECT / "Assets/FogRuntimeReference/FogMatterSimulation.cs").read_text(encoding="utf-8")
assert hashlib.sha256(frozen.replace("namespace BuildWorks.FogRuntimeReference", "namespace BuildWorks.FogStudy").encode()).hexdigest().upper() == "7576245AB043F81F739F5232DD361D9BB56EBBEC9F49D0B30247AD0C01D87150"
manifest = json.loads((ROOT / "artifacts/fog-runtime-assets-candidate/verification.json").read_text(encoding="utf-8-sig"))
assert manifest["passed"] is True and manifest["colorSpace"] == "Linear"
bundle = ROOT / "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"
assert hashlib.sha256(bundle.read_bytes()).hexdigest().upper() == manifest["sha256"]
for entry in manifest["sources"]:
    assert hashlib.sha256((ROOT / "src/BuildWorks/Assets/FogGhostRuntime" / entry["name"]).read_bytes()).hexdigest().upper() == entry["sha256"]
for dest in (PROJECT / "Assets/Editor/RuntimeSources").glob("*.cs"):
    source = ROOT / "src/BuildWorks" / dest.name
    if source.is_file():
        dest.write_bytes(source.read_bytes())
arena = PROJECT / "Assets/Editor/RuntimeSources/BlueprintFogArena.cs"
normal = arena.read_bytes()
settings = PROJECT / "ProjectSettings/ProjectSettings.asset"
settings_bytes = settings.read_bytes()
try:
    arena.write_bytes(b"#define BUILDWORKS_FOG_GPU_RESIDENT_CANDIDATE\n" + normal)
    launch("FogGpuResidencyCheck.Run", "candidate")
finally:
    arena.write_bytes(normal)
    settings.write_bytes(settings_bytes)
    # Recompile after restoring, even if the candidate failed; its cap must not remain the editor baseline.
    launch("FogGpuResidencyCheck.BaselineCompile", "baseline")
    settings.write_bytes(settings_bytes)
assert json.loads((OUT / "editor-baseline.json").read_text())["fieldCap"] == 98304000
proof = json.loads((OUT / "gpu-oracle.json").read_text())
assert proof["passed"] is True and proof["resident"]["residentCapacity"] == 32
assert arena.read_bytes() == normal and settings.read_bytes() == settings_bytes
assert processes() == 0
print("PASS isolated GPU oracle and ordinary editor restore", flush=True)

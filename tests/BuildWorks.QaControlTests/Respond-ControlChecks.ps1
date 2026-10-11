param([string]$ControlSource)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $ControlSource) { $ControlSource = Join-Path $root 'src/BuildWorks/BuildWorksQaControl.cs' }
$source = [IO.File]::ReadAllText($ControlSource)
$replyStart = $source.IndexOf('[Serializable] private sealed class Reply', [StringComparison]::Ordinal)
$replyEnd = $source.IndexOf('private readonly BuildWorksQaInput', $replyStart, [StringComparison]::Ordinal)
$methodStart = $source.IndexOf('private void Respond(', [StringComparison]::Ordinal)
$methodEnd = $source.IndexOf('internal void Stop(', $methodStart, [StringComparison]::Ordinal)
if ($replyStart -lt 0 -or $replyEnd -lt 0 -or $methodStart -lt 0 -or $methodEnd -lt 0) { throw 'Reply/Respond extraction failed.' }
# Execute the actual method with Unity destroyed-object semantics, without loading Unity or the game.
$harness = @'
using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace UnityEngine {
    public class Object {
        public bool alive = true;
        public string label = "live";
        public static implicit operator bool(Object value) { return value != null && value.alive; }
        protected void RequireAlive() { if (!alive) throw new NullReferenceException("Destroyed Unity object accessed."); }
        public string name { get { RequireAlive(); return label; } }
    }
    public struct Vector3 { public float x, y, z; }
    public class Transform { public Vector3 position, eulerAngles; }
    public class Camera : Object { public static Camera main; public Transform transform = new Transform(); }
    public static class Application { public static bool isFocused = true; }
    public static class Time { public static int frameCount = 17; }
    public static class Screen { public static int width = 3440, height = 1440; }
    public static class JsonUtility { public static object Last; public static string ToJson(object value, bool pretty) { Last = value; return "{}"; } }
}
namespace UnityEngine.EventSystems {
    public class EventSystem : UnityEngine.Object {
        public static EventSystem current;
        public UnityEngine.Object selected, module;
        public UnityEngine.Object currentSelectedGameObject { get { RequireAlive(); return selected; } }
        public UnityEngine.Object currentInputModule { get { RequireAlive(); return module; } }
    }
}
public class World { public string m_worldName = "BW_QA_test"; }
public class ZNet : UnityEngine.Object {
    public static ZNet instance;
    public World world = new World();
    public World GetWorld() { RequireAlive(); return world; }
}
public class Player : UnityEngine.Object { public static Player m_localPlayer; public Transform transform = new Transform(); }
public class QaReplyHarness {
    private string nonce = "0123456789abcdef0123456789abcdef";
    private int publications;
    private bool SafeSession() { return false; }
    private void Publish(string path, byte[] bytes) { if (path != "response.json") throw new Exception("Wrong path."); ++publications; }
__REPLY__
__METHOD__
    private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    public static int Run() {
        var host = new QaReplyHarness();
        ZNet.instance = new ZNet(); Player.m_localPlayer = new Player(); Camera.main = new Camera();
        EventSystem.current = new EventSystem();
        var selected = new UnityEngine.Object { label = "Import entry" };
        var module = new UnityEngine.Object();
        EventSystem.current.selected = selected; EventSystem.current.module = module;
        int count = 0;
        foreach (string status in new[] { "running", "completed", "aborted", "rejected", "uncertain" }) {
            host.Respond(321, status, "observed", "screenshot.png");
            var result = (Reply)JsonUtility.Last;
            Require(result.id == 321 && result.nonce == host.nonce && result.status == status &&
                result.detail == "observed" && result.screenshot == "screenshot.png" && result.selectedUi == "Import entry" &&
                result.uiModule == module.GetType().FullName && result.world == "BW_QA_test" && !result.ready &&
                result.width == 3440 && result.height == 1440 && result.frame == 17, "Live observation changed."); ++count;
        }
        selected.alive = false;
        host.Respond(322, "completed", "folder recreated", "screenshot.png");
        Require(((Reply)JsonUtility.Last).selectedUi == null && ((Reply)JsonUtility.Last).id == 322,
            "Destroyed selected UI prevented completion receipt."); ++count;
        module.alive = false;
        host.Respond(323, "aborted", "released");
        Require(((Reply)JsonUtility.Last).uiModule == null && ((Reply)JsonUtility.Last).screenshot == null,
            "Destroyed UI module prevented stop receipt."); ++count;
        EventSystem.current.alive = false;
        host.Respond(324, "uncertain", "capture failure");
        Require(((Reply)JsonUtility.Last).selectedUi == null && ((Reply)JsonUtility.Last).uiModule == null,
            "Destroyed EventSystem prevented failure receipt."); ++count;
        ZNet.instance.alive = false; Player.m_localPlayer.alive = false; Camera.main.alive = false;
        host.Respond(325, "completed", "unloaded");
        Require(((Reply)JsonUtility.Last).world == null, "Destroyed network accessed."); ++count;
        EventSystem.current = null; ZNet.instance = null; Player.m_localPlayer = null; Camera.main = null;
        host.Respond(326, "completed", "absent");
        Require(((Reply)JsonUtility.Last).selectedUi == null && ((Reply)JsonUtility.Last).uiModule == null &&
            ((Reply)JsonUtility.Last).world == null, "Absent scene references refused."); ++count;
        ZNet.instance = new ZNet { world = null };
        host.Respond(327, "completed", "world absent");
        Require(((Reply)JsonUtility.Last).world == null, "Absent world refused."); ++count;
        Require(host.publications == count, "Missing or duplicate receipt.");
        return count;
    }
}
'@
$harness = $harness.Replace('__REPLY__', $source.Substring($replyStart, $replyEnd - $replyStart)).Replace('__METHOD__', $source.Substring($methodStart, $methodEnd - $methodStart))
Add-Type -TypeDefinition $harness
$count = [QaReplyHarness]::Run()
"PASS actual Respond: $count live/destroyed/absent UI/network observations; exact request identity/status and one publication; CPU Unity-object shim, no native runtime."

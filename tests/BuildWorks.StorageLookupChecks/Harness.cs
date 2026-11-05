using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
namespace BuildWorksStorageLookupFixtures
{
using UnityEngine;
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; }
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public sealed class GameObject : Object { public string name; }
    public static class Time { public static int frameCount = 1; }
    public static class Application { public static bool isFocused = true; }
    public enum KeyCode { F12 }
    public static class Input { public static bool Emergency; public static bool GetKey(KeyCode key) => Emergency; }
}
internal sealed class LookupProjects { internal bool QaReadonlyIdle = true; }
internal static class BuildWorksPlugin
{
    internal static LookupProjects QaConstructionProjects = new LookupProjects();
    internal static bool StorageIdle = true;
    internal static void RequireQaStorageIdle() { if (!StorageIdle) throw new InvalidOperationException("editor or placement active"); }
}
internal sealed class LookupAdmission
{
    internal int ownerThread = Thread.CurrentThread.ManagedThreadId;
    internal object running, capture;
    internal UnityEngine.Object fog;
    internal bool StorageBusy;
    internal string Root;
    private bool StoragePersistenceBusy() => StorageBusy;
    private string PathFor(string name) => Path.Combine(Root, name);
// PRODUCTION_ADMISSION
    internal void Check() { RequireStorageIdle(); }
}
internal sealed class Player { public long Id = 33; public long GetPlayerID() => Id; }
internal sealed class PlayerProfile
{
    public long Id = 33; public string Path = "profile", Filename = "name";
    public long GetPlayerID() => Id;
    public string GetPath() => Path;
    public string GetFilename() => Filename;
}
internal sealed class World { public long m_uid = 77; public string Path = "world"; public string GetSaveFWLPath() => Path; }
internal sealed class Game : UnityEngine.Object
{
    public static Game instance; public PlayerProfile Profile = new PlayerProfile();
    public PlayerProfile GetPlayerProfile() => Profile;
}
internal sealed class ZNet
{
    public static ZNet instance; public World World = new World(); public bool Saving;
    public World GetWorld() => World; public bool IsSaving() => Saving;
}
internal enum SaveSystemSessionFlags { DontSaveCharacter, DontSaveWorld }
internal static class SaveSystem
{
    public static uint Generation = 8; public static bool Disabled;
    public static uint GetSaveNumber() => Generation;
    public static bool HasSessionFlag(SaveSystemSessionFlags flag) => Disabled;
}
internal static class BuildWorksTestIsolation
{
    public static Player Player = new Player(); public static bool Refuse;
    public static Player RequireLoadedSession() => Refuse ? throw new InvalidOperationException("isolation refused") : Player;
}
internal static class CompositeBlueprintStore { public static void ValidateExchangeDirectory(string path) { if (!Directory.Exists(path)) throw new Exception("missing directory"); } }
internal static class StableHash { public static int GetStableHashCode(this string name) => 12; }
internal static class BlueprintStorageTransferJournal
{
    internal enum Direction { ToContainer, ToPlayer }
    internal sealed class Plan { internal Guid Operation; internal Direction Route; internal long World, Player, Creator; internal int PrefabHash; internal string PieceName; }
    internal sealed class Snapshot { internal int Schema = 4; internal bool Settled; internal Plan Prepared; }
    internal static string Root;
    internal static Snapshot Saved;
    internal static Snapshot Read(string path) => Saved;
}
internal struct ZDOID { public long UserID; public uint ID; }
internal sealed class ZDO
{
    public ZDOID m_uid;
    public int Prefab = 12;
    public string Marker = "op";
    public long Creator = 33;
    public Vector3 Position;
    public bool Valid = true, Persistent = true;
    public uint DataRevision = 3;
    public bool IsValid() => Valid;
    public int GetPrefab() => Prefab;
    public string GetString(int key, string fallback) => Marker;
    public long GetLong(int key, long fallback) => Creator;
    public Vector3 GetPosition() => Position;
}
internal static class ZDOVars { public const int s_creator = 1; }
internal static class BlueprintStorageCapture { internal const int TransferMarkerKey = 2; }
internal sealed class ZNetScene : UnityEngine.Object
{
    internal static ZNetScene instance;
    internal GameObject Prefab = new GameObject { name = "chest" };
    internal GameObject GetPrefab(string name) => Prefab;
    internal readonly HashSet<ZDOID> Loaded = new HashSet<ZDOID>();
    internal object FindInstance(ZDOID id) => Loaded.Contains(id) ? this : null;
}
internal sealed class ZDOMan
{
    internal static ZDOMan instance;
    internal readonly Dictionary<ZDOID, ZDO> Rows = new Dictionary<ZDOID, ZDO>();
    internal readonly List<ZDO[]> Batches = new List<ZDO[]>();
    internal int Calls;
    internal bool Stuck, Forever, Throw;
    internal Action AfterCall;
    internal ZDO GetZDO(ZDOID id) => Rows.TryGetValue(id, out var row) ? row : null;
    internal int NrOfObjects() => Rows.Count;
    internal bool GetAllZDOsWithPrefabIterative(string prefab, List<ZDO> rows, ref int index)
    {
        if (Throw) throw new InvalidOperationException("native failure");
        if (Calls < Batches.Count) rows.AddRange(Batches[Calls]);
        ++Calls;
        if (!Stuck) ++index;
        AfterCall?.Invoke();
        return !Forever && Calls >= Batches.Count;
    }
}
public static class StorageLookupChecks
{
// PRODUCTION_TYPES
// PRODUCTION_CONTEXT
// PRODUCTION_SCAN
    private static int checks;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); ++checks; }
    private static ZDO Row(uint id) => new ZDO { m_uid = new ZDOID { UserID = 44, ID = id }, Position = new Vector3 { x = id, y = 2, z = 3 } };
    private static ZDOMan Manager(params ZDO[][] batches)
    {
        var manager = new ZDOMan(); manager.Batches.AddRange(batches);
        foreach (var row in batches.SelectMany(rows => rows)) if (row != null) manager.Rows[row.m_uid] = row;
        return manager;
    }
    private static StorageLookupResult Scan(ZDOMan manager, ZNetScene scene = null, Action guard = null) =>
        ScanStorageRegistry("op", "chest", 12, manager, scene ?? new ZNetScene(), guard ?? (() => { }));
    public static void Run()
    {
        // The excluded native context wrapper populates these fields.
        GC.KeepAlive(new StorageLookupResult { frame = 0, world = 0, player = 0, journalHash = "" });
        var a = Row(1); var b = Row(2); var scene = new ZNetScene();
        var result = Scan(Manager(new[] { a }));
        Require(result.complete && result.valid && result.uniqueMatches == 1 && !result.matches[0].sceneInstancePresent, "Unloaded registry target lost");
        Require(result.matches[0].userId == 44 && result.matches[0].id == 1 && result.matches[0].creator == 33 && result.matches[0].x == 1 && result.reason == "current-identity-found-no-recovery-authority", "Detached identity changed");
        scene.Loaded.Add(a.m_uid); result = Scan(Manager(new[] { a }), scene);
        Require(result.complete && result.matches[0].sceneInstancePresent, "Loaded target omitted");
        result = Scan(Manager(new[] { a }, new[] { a }));
        Require(result.complete && result.calls == 2 && result.rawRows == 2 && result.uniqueMatches == 1, "Boundary duplicate not deduplicated");
        result = Scan(Manager(new[] { a, b }));
        Require(result.complete && result.uniqueMatches == 2 && result.reason == "ambiguous-marker", "Ambiguous target silently selected");
        b.Marker = ""; result = Scan(Manager(new[] { b }));
        Require(result.complete && result.uniqueMatches == 0 && result.reason == "marker-not-found-in-current-registry", "Prefab-only target accepted");
        b.Marker = "OP"; Require(Scan(Manager(new[] { b })).uniqueMatches == 0, "Marker comparison is not ordinal");
        b.Marker = "op"; b.Prefab = 13; Require(Scan(Manager(new[] { b })).uniqueMatches == 0, "Marker-only target accepted");
        b.Prefab = 12;
        var manager = Manager(new[] { a }); manager.Forever = manager.Stuck = true; result = Scan(manager);
        Require(!result.complete && !result.valid && result.reason.Contains("no progress"), "False without progress accepted");
        manager = Manager(); manager.Forever = true; result = Scan(manager);
        Require(!result.complete && !result.valid && result.truncated && manager.Calls == 4096, "Call ceiling is not enforced");
        manager = Manager(Enumerable.Repeat(a, 100001).ToArray()); result = Scan(manager);
        Require(!result.complete && result.truncated && result.rawRows == 100001, "Row ceiling ignored");
        manager = Manager(Enumerable.Range(1, 17).Select(i => Row((uint)i)).ToArray()); result = Scan(manager);
        Require(!result.complete && !result.valid && result.truncated && result.uniqueMatches == 16, "Match ceiling ignored");
        manager = Manager(); manager.Throw = true; result = Scan(manager);
        Require(!result.complete && !result.valid && result.reason == "native failure", "Native exception hidden as not found");
        Require(!Scan(Manager(new ZDO[] { null })).valid, "Null native row accepted");
        b.Valid = false; Require(!Scan(Manager(new[] { b })).valid, "Invalid native row accepted"); b.Valid = true;
        b.Position = new Vector3 { x = float.NaN }; Require(!Scan(Manager(new[] { b })).valid, "Nonfinite position accepted");
        b.Position = new Vector3 { z = float.PositiveInfinity }; Require(!Scan(Manager(new[] { b })).valid, "Infinite position accepted");
        b = Row(2);
        result = Scan(Manager(new[] { a }), guard: () => { throw new InvalidOperationException("context failed"); });
        Require(!result.valid && result.calls == 0, "Context failure before native call ignored");
        int guards = 0;
        result = Scan(Manager(new[] { a }), guard: () => { if (++guards == 2) throw new InvalidOperationException("late context"); });
        Require(!result.valid && !result.complete && result.calls == 1, "Post-call context failure ignored");
        guards = 0;
        result = Scan(Manager(new[] { a }), guard: () => { if (++guards == 4) throw new InvalidOperationException("final context"); });
        Require(!result.valid && !result.complete, "Final context failure ignored");
        foreach (string field in new[] { "marker", "prefab", "position", "revision", "creator", "persistent", "loaded", "removed", "replaced" })
        {
            a = Row(1); manager = Manager(new[] { a }); scene = new ZNetScene(); guards = 0;
            result = Scan(manager, scene, () => {
                if (++guards != 3) return;
                switch (field) {
                    case "marker": a.Marker = "changed"; break;
                    case "prefab": a.Prefab = 13; break;
                    case "position": a.Position = new Vector3 { x = 9 }; break;
                    case "revision": ++a.DataRevision; break;
                    case "creator": ++a.Creator; break;
                    case "persistent": a.Persistent = false; break;
                    case "loaded": scene.Loaded.Add(a.m_uid); break;
                    case "removed": manager.Rows.Remove(a.m_uid); break;
                    case "replaced": manager.Rows[a.m_uid] = Row(1); break;
                }
            });
            Require(!result.complete && !result.valid, "Late match change accepted: " + field);
        }
        a = Row(1); b = Row(1); manager = Manager(new[] { a }, new[] { b });
        Require(!Scan(manager).valid, "Duplicate ID with different reference accepted");
        manager = Manager(new[] { Row(1) }); manager.AfterCall = () => System.Threading.Thread.Sleep(2010); result = Scan(manager);
        Require(!result.valid && result.truncated && result.calls == 1, "Post-native time ceiling ignored");
        ContextChecks();
        RunAdmission();
        Console.WriteLine("PASS storage registry lookup: " + checks + " checks; actual scanner with substituted native registry, no game proof.");
    }
    public static void RunAdmission()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuildWorks-storage-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string state in new[] { "none", "uninitialized", "thread", "running", "capture", "fog", "storage", "focus", "F12", "stop", "editor-placement", "projects", "projects-missing" })
            {
                var admission = new LookupAdmission { Root = root };
                Application.isFocused = true; UnityEngine.Input.Emergency = false; BuildWorksPlugin.StorageIdle = true;
                BuildWorksPlugin.QaConstructionProjects = new LookupProjects();
                switch (state) {
                    case "uninitialized": admission.ownerThread = 0; break;
                    case "thread": ++admission.ownerThread; break;
                    case "running": admission.running = new object(); break;
                    case "capture": admission.capture = new object(); break;
                    case "fog": admission.fog = new UnityEngine.Object(); break;
                    case "storage": admission.StorageBusy = true; break;
                    case "focus": Application.isFocused = false; break;
                    case "F12": UnityEngine.Input.Emergency = true; break;
                    case "stop": File.WriteAllText(Path.Combine(root, "stop.flag"), "stop"); break;
                    case "editor-placement": BuildWorksPlugin.StorageIdle = false; break;
                    case "projects": BuildWorksPlugin.QaConstructionProjects.QaReadonlyIdle = false; break;
                    case "projects-missing": BuildWorksPlugin.QaConstructionProjects = null; break;
                }
                bool refused = false; try { admission.Check(); } catch (InvalidOperationException) { refused = true; }
                Require(refused == (state != "none"), "Admission accepted " + state);
                if (state == "stop") {
                    Require(File.ReadAllText(Path.Combine(root, "stop.flag")) == "stop", "Admission cleared emergency flag");
                    File.Delete(Path.Combine(root, "stop.flag"));
                }
            }
            Application.isFocused = true; UnityEngine.Input.Emergency = false; BuildWorksPlugin.StorageIdle = true;
            BuildWorksPlugin.QaConstructionProjects = new LookupProjects();
            var owner = new LookupAdmission { Root = root }; bool offThreadRefused = false;
            var worker = new Thread(() => { try { owner.Check(); } catch (InvalidOperationException) { offThreadRefused = true; } });
            worker.Start(); worker.Join(); Require(offThreadRefused, "Admission accepted an actual other thread");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void ContextChecks()
    {
        string root = Path.Combine(Path.GetTempPath(), "BuildWorks-storage-lookup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); BlueprintStorageTransferJournal.Root = root;
        string operation = "9e2e54716c25dbc0eaca0f3fc35df029", path = Path.Combine(root, operation + ".bwtransfer");
        byte[] bytes = Enumerable.Repeat((byte)7, 64).ToArray(); File.WriteAllBytes(path, bytes);
        try
        {
            foreach (string failure in new[] { "none", "operation", "uppercase", "path", "prefab", "schema", "settled", "world", "payer", "creator", "hash", "piece", "direction", "isolation", "saving", "disabled", "profile", "generation", "frame", "registry", "world-ref", "profile-ref", "journal-writer" })
            {
                Game.instance = new Game(); ZNet.instance = new ZNet(); ZNetScene.instance = new ZNetScene();
                ZDOMan.instance = Manager(new[] { Row(1) }); SaveSystem.Disabled = false; SaveSystem.Generation = 8;
                BuildWorksTestIsolation.Refuse = false; BuildWorksTestIsolation.Player = new Player(); Time.frameCount = 1;
                var saved = new BlueprintStorageTransferJournal.Snapshot { Prepared = new BlueprintStorageTransferJournal.Plan {
                    Operation = Guid.ParseExact(operation, "N"), World = 77, Player = 33, Creator = 33, PrefabHash = 12, PieceName = "chest(Clone)" } };
                BlueprintStorageTransferJournal.Saved = saved;
                string op = operation, prefab = "chest"; int guards = 0; bool writerRefused = false;
                switch (failure) {
                    case "operation": saved.Prepared.Operation = Guid.NewGuid(); break;
                    case "uppercase": op = operation.ToUpperInvariant(); break;
                    case "path": op = "../" + operation; break;
                    case "prefab": ZNetScene.instance.Prefab.name = "wrong"; break;
                    case "schema": saved.Schema = 6; break;
                    case "settled": saved.Settled = true; break;
                    case "world": saved.Prepared.World = 88; break;
                    case "payer": saved.Prepared.Player = 88; break;
                    case "creator": saved.Prepared.Creator = 88; break;
                    case "hash": saved.Prepared.PrefabHash = 13; break;
                    case "piece": saved.Prepared.PieceName = "wrong"; break;
                    case "direction": saved.Prepared.Route = BlueprintStorageTransferJournal.Direction.ToPlayer; break;
                    case "isolation": BuildWorksTestIsolation.Refuse = true; break;
                    case "saving": ZNet.instance.Saving = true; break;
                    case "disabled": SaveSystem.Disabled = true; break;
                }
                StorageLookupResult resultForContext = null; bool threw = false;
                try {
                    resultForContext = LookupStorageMarker(op, prefab, () => {
                        if (++guards != 5) return;
                        switch (failure) {
                            case "profile": Game.instance.Profile.Path = "changed"; break;
                            case "generation": ++SaveSystem.Generation; break;
                            case "frame": ++Time.frameCount; break;
                            case "registry": ZDOMan.instance = Manager(); break;
                            case "world-ref": ZNet.instance.World = new World(); break;
                            case "profile-ref": Game.instance.Profile = new PlayerProfile(); break;
                            case "journal-writer":
                                try { using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } }
                                catch (IOException) { writerRefused = true; }
                                break;
                        }
                    });
                } catch (Exception) { threw = true; }
                if (failure == "none" || failure == "journal-writer") {
                    Require(!threw && resultForContext.complete && resultForContext.valid && resultForContext.world == 77 && resultForContext.player == 33 && resultForContext.journalHash.Length == 64,
                        "Valid readonly context refused: " + failure);
                    if (failure == "journal-writer") Require(writerRefused, "Read lock allowed a concurrent writer");
                } else Require(threw || resultForContext != null && !resultForContext.complete && !resultForContext.valid, "Invalid WAL/context accepted: " + failure);
                Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Lookup changed journal: " + failure);
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
}

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;

// Actual production writer/getter/setter patches; inert ZDO storage, real Unity quaternion math.
public static class RotationPersistenceAcceptance
{
    private static int assertions;
    private static readonly FieldInfo Native = AccessTools.Field(typeof(ZDO), "m_rotation");
    private static readonly int Exact = (int)AccessTools.Field(typeof(BlueprintRotationState), "ExactKey").GetValue(null);
    private static readonly int Witness = (int)AccessTools.Field(typeof(BlueprintRotationState), "WitnessKey").GetValue(null);

    public static void Run()
    {
        var harmony = new Harmony("BuildWorks.RotationPersistenceAcceptance");
        try
        {
            harmony.CreateClassProcessor(typeof(BlueprintRotationState.ReadPatch)).Patch();
            harmony.CreateClassProcessor(typeof(BlueprintRotationState.ChangePatch)).Patch();
            // Compile callers after patching, matching BepInEx startup before world creation.
            typeof(RotationPersistenceAcceptance).GetMethod(nameof(CheckAll), BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
        finally { harmony.UnpatchSelf(); }
    }

    private static void CheckAll()
    {
        var roots = new List<GameObject>();
        try
        {
            GameObject Make(Quaternion q)
            {
                var root = new GameObject("RotationFixture", typeof(Piece), typeof(ZNetView));
                roots.Add(root); root.transform.rotation = q;
                root.GetComponent<ZNetView>().GetZDO().m_uid = new ZDOID { UserKey = 1, ID = (uint)roots.Count };
                root.GetComponent<ZNetView>().GetZDO().SetRotation(q);
                return root;
            }
            var source = Make(Quaternion.identity); var ghost = Make(Quaternion.identity);
            foreach (Vector3 euler in new[] { new Vector3(0, 224.99997f, 0), new Vector3(16.2498f, 224.99997f, 41.99998f), new Vector3(359.99997f, 75.2498f, 0) })
            {
                var placed = Make(Quaternion.Euler(euler)); var data = placed.GetComponent<ZNetView>().GetZDO();
                Native.SetValue(data, euler);
                Quaternion expected = placed.transform.rotation;
                ZDO Reload(bool extras)
                {
                    var copy = new ZDO { m_uid = data.m_uid };
                    var packet = new ZPackage(); packet.WriteSmallRotation((Vector3)Native.GetValue(data));
                    Native.SetValue(copy, new ZPackage(packet.GetArray()).ReadSmallRotation());
                    if (extras)
                        foreach (int key in new[] { Exact, Witness })
                        { Require(data.GetQuaternion(key, out Quaternion q), "persisted extra exists"); copy.Set(key, q); }
                    return copy;
                }
                Require(Quaternion.Angle(Reload(false).GetRotation(), expected) > .05f, "native quantization control loses angle");
                BlueprintRotationState.ApplyCreated(placed, source, ghost);
                var reloaded = Reload(true); int writes = reloaded.Writes;
                Require(BlueprintRotationState.TryRestore(reloaded, Quaternion.Euler((Vector3)Native.GetValue(reloaded)), out _), "actual reader accepts stored witness");
                Equal(reloaded.GetRotation(), expected, "patched read retains precise rotation on new data");
                Require(reloaded.Writes == writes, "getter never writes");
                reloaded.SetRotation(new Quaternion(-expected.x, -expected.y, -expected.z, -expected.w));
                Require(reloaded.GetQuaternion(Exact, out _), "same rotation with opposite sign retains record");
                reloaded.SetRotation(Quaternion.Euler(0, 90.25f, 0));
                Require(!reloaded.GetQuaternion(Exact, out _), "real setter change invalidates extra");
                Require(!reloaded.GetQuaternion(Witness, out _), "invalidated witness removed too");
                Equal(reloaded.GetRotation(), Quaternion.Euler(0, 90.25f, 0), "manual rotation wins");
                var stale = Reload(true); Native.SetValue(stale, new Vector3(0, 90, 0));
                Equal(stale.GetRotation(), Quaternion.Euler(0, 90, 0), "stale witness falls back before marker removal");
                foreach (Quaternion bad in new[] { new Quaternion(), new Quaternion(0, 0, 0, 2), new Quaternion(0, 0, 0, .9999f), new Quaternion(float.NaN, 0, 0, 1), new Quaternion(float.PositiveInfinity, 0, 0, 1) })
                {
                    var malformed = Reload(true); malformed.Set(Exact, bad);
                    Require(!BlueprintRotationState.TryRestore(malformed, Quaternion.Euler(euler), out _), "malformed exact rejected");
                }
                var noWitness = Reload(true); noWitness.RemoveQuaternion(Witness);
                Require(!BlueprintRotationState.TryRestore(noWitness, expected, out _), "partial record rejected");
                var badWitness = Reload(true); badWitness.Set(Witness, new Quaternion());
                Require(!BlueprintRotationState.TryRestore(badWitness, expected, out _), "malformed witness rejected");
                Reject(() => BlueprintRotationState.ApplyCreated(placed, source, ghost), "existing record must not be overwritten");
            }
            var fresh = Make(Quaternion.Euler(0, 45, 0)); var view = fresh.GetComponent<ZNetView>();
            Reject(() => BlueprintRotationState.ApplyCreated(source, source, ghost), "source rejected");
            Reject(() => BlueprintRotationState.ApplyCreated(ghost, source, ghost), "ghost rejected");
            Reject(() => BlueprintRotationState.ApplyCreated(null, source, ghost), "missing created object rejected");
            Reject(() => BlueprintRotationState.ApplyCreated(fresh, source, null), "lost ghost rejected");
            view.Owner = false; Reject(() => BlueprintRotationState.ApplyCreated(fresh, source, ghost), "unowned rejected"); view.Owner = true;
            view.Valid = false; Reject(() => BlueprintRotationState.ApplyCreated(fresh, source, ghost), "invalid view rejected"); view.Valid = true;
            ZDOID savedId = view.GetZDO().m_uid;
            view.GetZDO().m_uid = default; Reject(() => BlueprintRotationState.ApplyCreated(fresh, source, ghost), "empty identity rejected");
            view.GetZDO().m_uid = source.GetComponent<ZNetView>().GetZDO().m_uid;
            Reject(() => BlueprintRotationState.ApplyCreated(fresh, source, ghost), "shared source identity rejected");
            view.GetZDO().m_uid = ghost.GetComponent<ZNetView>().GetZDO().m_uid;
            Reject(() => BlueprintRotationState.ApplyCreated(fresh, source, ghost), "shared ghost identity rejected");
            view.GetZDO().m_uid = savedId;
            ZNetView.m_useInitZDO = true;
            Reject(BlueprintRotationState.RequireFreshCreation, "borrowed native creation rejected");
            ZNetView.m_useInitZDO = false; ZNetView.m_initZDO = new ZDO();
            Reject(BlueprintRotationState.RequireFreshCreation, "pending native identity rejected");
            ZNetView.m_initZDO = null; BlueprintRotationState.RequireFreshCreation();
            Equal(view.GetZDO().GetRotation(), fresh.transform.rotation, "unmarked native object unchanged");
            Require(!view.GetZDO().GetQuaternion(Exact, out _), "failed writes left no exact record");
            Debug.Log("PASS RotationPersistenceAcceptance assertions=" + assertions + "; inert persistence only, native save/network not executed");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally
        {
            ZNetView.m_useInitZDO = false; ZNetView.m_initZDO = null;
            foreach (GameObject root in roots) if (root) UnityEngine.Object.DestroyImmediate(root);
        }
    }
    private static void Equal(Quaternion actual, Quaternion expected, string label)
    {
        float sign = Quaternion.Dot(actual, expected) < 0 ? -1 : 1;
        float error = Math.Max(Math.Max(Math.Abs(actual.x - sign * expected.x), Math.Abs(actual.y - sign * expected.y)),
            Math.Max(Math.Abs(actual.z - sign * expected.z), Math.Abs(actual.w - sign * expected.w)));
        Require(error < 1e-6f, label + ": " + actual.ToString("R") + " vs " + expected.ToString("R") + ", error=" + error);
    }
    private static void Reject(Action action, string label)
    {
        try { action(); } catch (InvalidOperationException) { Require(true, label); return; }
        Require(false, label);
    }
    private static void Require(bool ok, string label)
    { assertions++; if (!ok) throw new InvalidOperationException("Rotation persistence regression: " + label); }
}

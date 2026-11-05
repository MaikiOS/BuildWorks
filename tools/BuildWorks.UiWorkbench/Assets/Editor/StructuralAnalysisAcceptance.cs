using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class StructuralAnalysisAcceptance
{
    internal static IEnumerable Run()
    {
        var sources = new List<GameObject>();
        GameObject Source(string name, bool lowBox = false)
        {
            var item = new GameObject(name); item.SetActive(false); sources.Add(item);
            item.AddComponent<BoxCollider>().center = Vector3.up * (lowBox ? -.5f : .5f);
            item.AddComponent<WearNTear>().m_comOffset = Vector3.up * .5f;
            return item;
        }
        var wood = Source("GraphWood");
        var lowBase = Source("GraphLowBase", true);
        var stone = Source("GraphStone");
        var stoneModel = stone.GetComponent<WearNTear>();
        stoneModel.FixtureMaximum = 1000; stoneModel.FixtureMinimum = 100; stoneModel.FixtureHorizontalLoss = 1;
        var snapshot = new BlueprintStructuralSnapshot();
        BlueprintStructuralAnalysis analysis = null;
        BlueprintStructuralSnapshot cancelledSnapshot = null;
        BlueprintStructuralAnalysis cancelled = null;
        try
        {
            void Add(string id, GameObject prefab, float x, float y) =>
                snapshot.Add(id, prefab, new Vector3(x, y, 0), Quaternion.identity, Vector3.one);
            Add("stackBase", wood, 0, 0); Add("stackUpper", wood, 0, 1); Add("stackTop", wood, 0, 2);
            Add("floatingA", wood, 10, 5); Add("floatingB", wood, 10.8f, 5);
            wood.GetComponent<WearNTear>().FixtureMaximum = float.NaN;
            Add("unknownBase", wood, 20, 0);
            wood.GetComponent<WearNTear>().FixtureMaximum = 100;
            Add("unknownUpper", wood, 20, 1);
            Add("cascadeBase", lowBase, 30, 1); Add("cascadeStone", stone, 30.7f, 1); Add("cascadeUpper", wood, 31.4f, 1);
            stoneModel.m_noSupportWear = false;
            Add("persistentBase", lowBase, 50, 1); Add("persistentStone", stone, 50.7f, 1); Add("persistentUpper", wood, 51.4f, 1);
            stoneModel.m_noSupportWear = true;
            Add("alternateBase", lowBase, 100, 1); Add("alternateStone", stone, 100.7f, 1); Add("alternateUpper", wood, 100.2f, 1);
            var badComSource = Source("GraphOverflowCom");
            badComSource.GetComponent<WearNTear>().m_comOffset = new Vector3(float.MaxValue, float.MaxValue, 0);
            snapshot.Add("badCom", badComSource, new Vector3(70, 0, 0), Quaternion.Euler(0, 0, 45), Vector3.one);
            analysis = new BlueprintStructuralAnalysis(snapshot, 0);
            int frames = 0;
            while (!analysis.Step(128, 8) && frames++ < 1000) yield return null;
            Require(analysis.Complete, "Whole-structure analysis failed to finish");
            var nodes = analysis.Nodes.ToDictionary(node => node.Part.Id);
            Require(nodes["stackBase"].State == BlueprintStructuralState.Supported && nodes["stackBase"].Result.Support == 100 &&
                nodes["stackBase"].PrimaryId == "@ground", "Ground foundation result/witness lost");
            Require(Mathf.Abs(nodes["stackUpper"].Result.Support - 86.25f) < .001f &&
                Mathf.Abs(nodes["stackTop"].Result.Support - 74.390625f) < .001f &&
                nodes["stackTop"].State == BlueprintStructuralState.Supported && nodes["stackTop"].PrimaryId == "stackUpper",
                "Support chain did not propagate from its foundation");
            foreach (string id in new[] { "floatingA", "floatingB" })
                Require(nodes[id].State == BlueprintStructuralState.Unsupported && nodes[id].WouldFall &&
                    !nodes[id].Part.Root.activeSelf, "Unanchored cycle invented support");
            Require(nodes["unknownBase"].State == BlueprintStructuralState.Unknown &&
                nodes["unknownUpper"].State == BlueprintStructuralState.Unknown && nodes["unknownUpper"].ColorValue == null,
                "Unknown source material became a green support chain");
            Require(nodes["cascadeStone"].State == BlueprintStructuralState.Unsupported && nodes["cascadeStone"].WouldFall &&
                nodes["cascadeUpper"].State == BlueprintStructuralState.Unsupported && nodes["cascadeUpper"].WouldFall &&
                nodes["cascadeUpper"].LostSupportId == "cascadeStone" && nodes["cascadeUpper"].Reason == "support-cascade",
                "Removing an unsupported native-wear source did not propagate its collapse");
            Require(nodes["persistentStone"].State == BlueprintStructuralState.Unsupported && !nodes["persistentStone"].WouldFall &&
                nodes["persistentStone"].Part.Root.activeSelf && nodes["persistentUpper"].State == BlueprintStructuralState.Supported,
                "No-support-wear=false was treated as structural destruction");
            Require(nodes["badCom"].State == BlueprintStructuralState.Unknown && nodes["badCom"].ColorValue == null,
                "Rotated overflowing COM did not fail closed to Unknown");
            Require(nodes["alternateUpper"].State == BlueprintStructuralState.Supported &&
                nodes["alternateUpper"].PrimaryId == "alternateBase" && nodes["alternateUpper"].LostSupportId == null,
                "Removed non-winning neighbor was reported as a causal lost support");
            Require(sources.All(source => !source.activeSelf && source.transform.position == Vector3.zero),
                "Analysis changed its source prefabs");
            cancelledSnapshot = new BlueprintStructuralSnapshot();
            cancelledSnapshot.Add("cancel", wood, Vector3.up * 5, Quaternion.identity, Vector3.one);
            cancelled = new BlueprintStructuralAnalysis(cancelledSnapshot, 0);
            cancelled.Step(1, 1); cancelled.Dispose();
            bool rejected = false;
            try { cancelled.Step(); } catch (ObjectDisposedException) { rejected = true; }
            Require(rejected, "Cancelled analysis accepted another step");
        }
        finally
        {
            analysis?.Dispose(); if (analysis == null) snapshot.Dispose();
            cancelled?.Dispose(); if (cancelled == null) cancelledSnapshot?.Dispose();
            foreach (GameObject source in sources) Object.DestroyImmediate(source);
        }
        while (snapshot.Unloading != null && !snapshot.Unloading.isDone) yield return null;
        while (cancelledSnapshot?.Unloading != null && !cancelledSnapshot.Unloading.isDone) yield return null;
        Require(!snapshot.IsSceneLoaded && (cancelledSnapshot == null || !cancelledSnapshot.IsSceneLoaded),
            "Analysis completion/cancellation leaked private physics scenes");
        Debug.Log("BUILDWORKS_STRUCTURAL_ANALYSIS_OK fixture physics/scalars;not native Valheim parity");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Structural analysis: " + message); }
}

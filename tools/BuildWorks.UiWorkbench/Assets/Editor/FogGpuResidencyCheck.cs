using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Correctness oracle only: comparison readbacks deliberately stay outside performance claims.
public static class FogGpuResidencyCheck
{
    public static void Run()
    {
        Require(BlueprintFogArena.MaximumFieldBytes == 268435456, "Explicit editor GPU candidate was not compiled.");
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/checkpoint-247");
        EditorSceneManager.OpenScene("Assets/FogGhostLab/FogWallStudy.unity"); PlayerSettings.colorSpace = ColorSpace.Linear;
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuildWorks.FogStudy.FogGhostLab")).First(t => t != null);
        var lab = (Behaviour)UnityEngine.Object.FindFirstObjectByType(type);
        var original = (GameObject)type.GetField("solidBuilding").GetValue(lab);
        var ghost = (GameObject)type.GetField("ghostBuilding").GetValue(lab);
        var actor = (Transform)type.GetField("actor").GetValue(lab); Bounds bounds = actor.GetComponent<Collider>().bounds;
        var body = new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y);
        lab.enabled = false; original.SetActive(false); ghost.SetActive(false);
        using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
        using (var source = new BlueprintFogVisual(assets, original, Vector3.zero, Quaternion.identity, Vector3.one, null, Vector3.zero, new Vector3(-1.8f, .95f, .03f), body))
        using (var codec = new FogSparseStorageProbe())
        {
            var paged = Compare(assets, source, body, codec, 4);
            var resident = Compare(assets, source, body, codec, 0);
            int lifecycle = FogRuntimeChannelLifecycleCheck.Run(assets, source);
            var proof = new Report { passed = true, gameHostTested = false, performanceMeasured = false,
                fieldCap = BlueprintFogArena.MaximumFieldBytes, lifecycleCases = lifecycle, paged = paged, resident = resident };
            File.WriteAllText(Path.Combine(output, "gpu-oracle.json"), JsonUtility.ToJson(proof, true));
            Debug.Log("PASS GPU residency: paged and resident exact v11 fields/history; lifecycle=" + lifecycle);
        }
    }

    private static Row Compare(BlueprintFogAssets assets, BlueprintFogVisual source, Vector2 body, FogSparseStorageProbe codec, int residentLimit)
    {
        int[] slots = { 0, 13, 27 }; int[] checkpoints = { 48, 240, 288, 1800 };
        BlueprintFogInputQueue.Sample Sample(int tick)
        {
            bool contact = tick <= 48 || (tick > 240 && tick <= 288);
            float x = tick <= 48 ? Mathf.Lerp(-1.8f, .4f, tick / 48f) : tick <= 240 ? 4f :
                tick <= 288 ? Mathf.Lerp(.4f, -1.8f, (tick - 240) / 48f) : 4f;
            return new BlueprintFogInputQueue.Sample(tick * BlueprintFogSimulation.Step, new Point3(x, .95f, .03f), body.x, body.y, true, contact, true);
        }
        Vector3 Actor(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
        BlueprintFogSimulation Make(int slot, BlueprintFogScratch scratch = null, BlueprintFogSimulation.StagedState staged = null)
            => new BlueprintFogSimulation(assets, source.Source.Shape, source.Source.Pigment, source.Source.Noise,
                source.FieldMinimum, source.FieldMaximum, source.Source.Minimum, source.Source.Maximum, Actor(Sample(0)), 37 + slot,
                scratch: scratch, stagedState: staged, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D);
        void Drain(BlueprintFogChannels pool) { AsyncGPUReadback.WaitAllRequests(); Require(pool.PollSnapshots(), "Receipt did not drain."); }
        var references = new BlueprintFogSimulation[slots.Length]; var row = new Row();
        var velocities = new RenderTexture[28]; Texture3D upload = null;
        try
        {
            using (var pool = BlueprintFogChannels.CreateArena(assets, 32, 32768, residentOwners: residentLimit, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
            {
                pool.EnablePerformanceProbe(); upload = pool.SnapshotArena.VelocityUpload;
                row.residentCapacity = pool.SnapshotArena.ResidentCapacity; row.reservedBytes = pool.SnapshotArena.ReservedFieldBytes;
                Require(row.residentCapacity == (residentLimit == 0 ? 32 : 4), "Unexpected resident capacity.");
                Require((bool)upload == (residentLimit != 0), "Upload reservation disagrees with residency.");
                var owners = new BlueprintFogChannels.SnapshotChannel[28];
                for (int i = 0; i < owners.Length; ++i) { int slot = i; owners[i] = pool.AcquireSnapshot(slot, Sample(0), (scratch, staged) => Make(slot, scratch, staged)); velocities[i] = owners[i].Velocity; }
                Require(pool.Count == 28 && pool.ResidentSnapshotCount == (residentLimit == 0 ? 28 : 4), "House owners were not admitted.");
                row.activeOwners = pool.Count;
                for (int i = 0; i < references.Length; ++i) references[i] = Make(slots[i]);
                var scalars = new[] { "previous", "inputPosition", "inputTime", "tick", "emissionRevision" }
                    .Select(name => typeof(BlueprintFogSimulation).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)).ToArray();
                for (int tick = 8; tick <= 1800; tick += 8)
                    for (int i = 0; i < slots.Length; ++i)
                    {
                        var owner = owners[slots[i]]; var sample = Sample(tick);
                        pool.CaptureSnapshot(owner, sample);
                        for (int drain = 0; drain < 64 && !pool.EnsureSnapshotVelocity(owner); ++drain) Drain(pool);
                        Require(owner.Velocity && !owner.evictionPending, "Owner residency failed.");
                        Require(pool.TrySubmitSnapshot(owner, out var token), "History submission refused.");
                        references[i].AdvanceTo(sample.Clock, Actor(sample), body, sample.Emit, sample.EmissionPossible);
                        Drain(pool);
                        Require(owner.State.State == BlueprintFogSnapshotState.Phase.Ready && owner.Inputs.Count == 0 &&
                            owner.State.ConfirmedTick == tick && owner.LastSubmission.Sequence == token.Sequence && owner.LastFlags == 0,
                            "Confirmed history/receipt differs.");
                        foreach (var field in scalars) Require(Equals(field.GetValue(owner.simulation), field.GetValue(references[i])), "CPU integrator differs: " + field.Name);
                        row.scalarComparisons += scalars.Length;
                        if (!checkpoints.Contains(tick)) continue;
                        pool.BindSnapshot(owner); pool.SnapshotArena.Dispatch("BeginStep"); pool.SnapshotArena.Dispatch("RestoreFields", 24, 20, 20);
                        var fields = pool.SnapshotArena.Scratch;
                        codec.BeginComparison(); codec.Compare(fields.ReadColour, references[i].Smoke);
                        codec.Compare(fields.ReadOrigin, FogRuntimeSparseCheck.Current(references[i], "origin"));
                        codec.Compare(fields.ReadSurface, references[i].Surface); codec.Compare(owner.Velocity, references[i].Flow);
                        for (int f = 0; f < 3; ++f) codec.Compare(owner.Ledger[f], FogRuntimeSparseCheck.Buffer(references[i], new[] { "away", "peak", "state" }[f]));
                        codec.RequireClean(); row.fieldComparisons += 7;
                        if (tick == 48 || tick == 288)
                        {
                            Require(FogRuntimeSparseCheck.Read(fields.ReadColour).Any(value => value != 0), "Active control contains no smoke."); ++row.activeControls;
                        }
                        if (tick == 1800)
                        {
                            Require(FogRuntimeSparseCheck.Read(fields.ReadColour).All(value => value == 0) &&
                                FogRuntimeSparseCheck.Read(fields.ReadSurface).All(value => value == 0), "Release did not recover."); ++row.recoveredControls;
                        }
                    }
                row.counters = pool.ReadPerformanceProbe(); Require(row.counters.valid, "Observer is invalid.");
                var velocity = row.counters.flows[(int)FogPerformanceCounters.Kind.Velocity];
                var receipt = row.counters.flows[(int)FogPerformanceCounters.Kind.Receipt];
                Require(receipt.requested == receipt.callbacks && receipt.callbacks == receipt.accepted && receipt.rejected == 0 &&
                    receipt.awaitingCallback == 0 && receipt.awaitingAcceptance == 0, "Receipt accounting is not balanced.");
                if (residentLimit == 0) Require(!upload && pool.VelocityRamBytes == 0 && velocity.requested == 0 && velocity.cpuCopiedBytes == 0 &&
                    row.counters.stages[(int)FogPerformanceCounters.Stage.Upload].count == 0, "Resident path still transfers velocity through CPU.");
                else Require(velocity.requested > 0 && velocity.cpuCopiedBytes > 0 && row.counters.stages[(int)FogPerformanceCounters.Stage.Upload].count > 0,
                    "Paged control did not exercise CPU transfers.");
                pool.Dispose(); Require(pool.Count == 0 && pool.FieldBytes == 0 && pool.VelocityRamBytes == 0, "Pool did not release memory.");
                Require(owners.All(owner => !owner.Velocity), "Owner retained a GPU velocity texture.");
            }
            Require(!upload && velocities.All(texture => !texture), "Initial GPU allocations leaked."); row.drained = true;
        }
        finally { foreach (var reference in references) reference?.Dispose(); }
        return row;
    }

    public static void BaselineCompile()
    {
        Require(BlueprintFogArena.MaximumFieldBytes == 98304000 && BlueprintFogChannels.MaximumFieldBudget == 98304000,
            "Candidate cap leaked into the ordinary editor.");
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        File.WriteAllText(Path.Combine(root, "artifacts/checkpoint-247/editor-baseline.json"), "{\"passed\":true,\"fieldCap\":98304000}");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Row
    {
        public int residentCapacity, activeOwners, scalarComparisons, fieldComparisons, activeControls, recoveredControls;
        public long reservedBytes; public bool drained; public FogPerformanceCounters.Snapshot counters;
    }
    [Serializable] private sealed class Report
    {
        public bool passed, gameHostTested, performanceMeasured; public long fieldCap; public int lifecycleCases; public Row paged, resident;
    }
}

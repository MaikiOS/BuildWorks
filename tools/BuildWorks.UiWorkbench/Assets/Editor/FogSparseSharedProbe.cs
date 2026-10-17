using System;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEngine;

// Diagnostic borrowing only. Production lifetime, render publication and allocator are not implemented here.
internal static class FogSparseSharedProbe
{
    private static readonly string[] Fields = { "colour", "origin", "surface" };
    private const int Owners = 8;
    private const long Budget = 98304000;

    internal static Report Run(FogSparseStorageProbe codec, BlueprintFogAssets assets, BlueprintFogVisual visual)
    {
        var reference = new BlueprintFogSimulation[Owners]; var candidate = new BlueprintFogSimulation[Owners];
        var stored = new FogSparseStorageProbe.Encoded[Owners][]; var reads = new RenderTexture[3];
        var grid = new Vector3Int(96, 80, 80);
        long dense = 0, peak = 0, peakEncoded = 0; int fields = 0, ledgers = 0, cpu = 0;
        bool distinct = false;
        var body = new Vector2(.3f, .95f);
        using (var scratch = new BlueprintFogScratch(grid))
        {
            BlueprintFogSimulation Make(int owner, BlueprintFogScratch temporary = null)
                => new BlueprintFogSimulation(assets, visual.Source.Shape, visual.Source.Pigment, visual.Source.Noise,
                    visual.FieldMinimum, visual.FieldMaximum, visual.Source.Minimum, visual.Source.Maximum,
                    Actor(owner, 0), 37 + owner, scratch: temporary);
            long EncodedBytes() => stored.Where(s => s != null).Sum(s => s.Where(v => v != null).Sum(v => v.Bytes));
            void Compare(int owner)
            {
                codec.BeginComparison();
                foreach (string name in new[] { "colour", "velocity", "origin", "surface" })
                { codec.Compare(FogRuntimeSparseCheck.Current(candidate[owner], name), FogRuntimeSparseCheck.Current(reference[owner], name)); ++fields; }
                foreach (string name in new[] { "away", "peak", "state" })
                { codec.Compare(FogRuntimeSparseCheck.Buffer(candidate[owner], name), FogRuntimeSparseCheck.Buffer(reference[owner], name)); ++ledgers; }
                codec.RequireClean();
            }
            try
            {
                // Encode and release each initial dense state before allocating the shared read textures.
                for (int i = 0; i < Owners; ++i)
                {
                    reference[i] = Make(i); candidate[i] = Make(i, scratch);
                    long constructing = scratch.FieldBytes + (i + 1L) * 4915200 + 14745600 +
                        (i + 1L) * 8196 + EncodedBytes() + codec.CommonBufferBytes;
                    stored[i] = new FogSparseStorageProbe.Encoded[3];
                    for (int f = 0; f < Fields.Length; ++f) stored[i][f] = codec.Encode(FogRuntimeSparseCheck.Current(candidate[i], Fields[f]), Budget);
                    peak = Math.Max(peak, constructing + stored[i].Sum(s => s.Bytes));
                    for (int f = 0; f < Fields.Length; ++f)
                    {
                        var pair = Pair(candidate[i], Fields[f]); RenderTexture old = pair[0]; pair[0] = null;
                        old.Release(); UnityEngine.Object.DestroyImmediate(old);
                    }
                }
                for (int f = 0; f < reads.Length; ++f) reads[f] = BlueprintFogSimulation.AllocateField(grid, "Sparse shared read " + f);
                for (int i = 0; i < Owners; ++i) for (int f = 0; f < Fields.Length; ++f) Pair(candidate[i], Fields[f])[0] = reads[f];
                dense = scratch.FieldBytes + reads.Sum(t => (long)t.width * t.height * t.volumeDepth * 8) +
                    candidate.Sum(s => { var t = s.Flow; return (long)t.width * t.height * t.volumeDepth * 8; });
                Require(dense == 83558400 && candidate.All(s => ReferenceEquals(s.Smoke, reads[0]) && ReferenceEquals(s.Surface, reads[2])),
                    "Sparse probe did not actually share its nine-texture workspace.");
                for (int tick = 0; tick <= 360; ++tick)
                {
                    // Both visit orders must work; owner7 initially advances without emission.
                    for (int visit = 0; visit < Owners; ++visit)
                    {
                        int i = tick % 2 == 0 ? visit : Owners - visit - 1;
                        for (int f = 0; f < Fields.Length; ++f) codec.Restore(stored[i][f], reads[f]);
                        bool emit = i != Owners - 1 || tick >= 200;
                        float time = tick * BlueprintFogSimulation.Step;
                        reference[i].AdvanceTo(time, Actor(i, time), body, emit);
                        candidate[i].AdvanceTo(time, Actor(i, time), body, emit);
                        Compare(i);
                        var next = new FogSparseStorageProbe.Encoded[3];
                        try
                        {
                            for (int f = 0; f < Fields.Length; ++f) next[f] = codec.Encode(reads[f], Budget);
                            long encoded = EncodedBytes() + next.Sum(s => s.Bytes);
                            peakEncoded = Math.Max(peakEncoded, encoded);
                            peak = Math.Max(peak, dense + Owners * 8196 + codec.CommonBufferBytes + encoded);
                            Require(peak <= Budget, "Shared measurement exceeded its candidate field budget.");
                            foreach (var old in stored[i]) old.Dispose(); stored[i] = next; next = null;
                        }
                        finally { if (next != null) foreach (var state in next) state?.Dispose(); }
                    }
                    if (tick == 165)
                    {
                        codec.BeginComparison(); codec.Compare(reference[0].Smoke, reference[1].Smoke);
                        try { codec.RequireClean(); } catch (InvalidOperationException) { distinct = true; }
                        Require(distinct, "Shared-state fixture did not distinguish two owners.");
                    }
                }
                // Every owner must still be recoverable after another owner used the workspace last.
                for (int i = 0; i < Owners; ++i)
                {
                    for (int f = 0; f < Fields.Length; ++f) codec.Restore(stored[i][f], reads[f]);
                    Compare(i);
                    foreach (string name in new[] { "colour", "surface" })
                    {
                        Require(FogRuntimeSparseCheck.Read(FogRuntimeSparseCheck.Current(candidate[i], name)).SequenceEqual(
                            FogRuntimeSparseCheck.Read(FogRuntimeSparseCheck.Current(reference[i], name))), "Shared CPU raw state differs."); ++cpu;
                    }
                }
                Require(fields == 11584 && ledgers == 8688 && cpu == 16 && distinct, "Shared-state coverage incomplete.");
                return new Report { passed = true, owners = Owners, ticks = 360, fieldComparisons = fields,
                    ledgerComparisons = ledgers, cpuRawComparisons = cpu, independentStates = distinct,
                    denseWorkspaceAndVelocityBytes = dense, peakCandidateFieldBytes = peak, peakEncodedOldNewBytes = peakEncoded,
                    productionAllocatorTested = false, rendererTested = false, sourceAdmissionTested = false,
                    note = "Reflection rebinds three reads; all physics kernels are unchanged. Reference simulations/source/atlas/driver staging excluded from candidate field bytes. Synchronous count reads/allocations are diagnostic only." };
            }
            finally
            {
                // Simulations own their velocity/ledger, never the borrowed read textures or scratch.
                foreach (var simulation in candidate) if (simulation != null)
                {
                    for (int f = 0; f < Fields.Length; ++f)
                    {
                        var pair = Pair(simulation, Fields[f]);
                        if (reads.Any(t => ReferenceEquals(t, pair[0]))) pair[0] = null;
                    }
                    simulation.Dispose();
                }
                foreach (var simulation in reference) simulation?.Dispose();
                foreach (var states in stored) if (states != null) foreach (var state in states) state?.Dispose();
                foreach (var texture in reads) if (texture) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            }
        }
    }
    private static RenderTexture[] Pair(BlueprintFogSimulation simulation, string name)
        => (RenderTexture[])typeof(BlueprintFogSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation);
    private static Vector3 Actor(int owner, float time)
    {
        float local = time - owner * .07f;
        float x = local < 3 ? Mathf.Lerp(-1.8f, 0, Mathf.Clamp01(local - 2)) : local < 4.5f ? 0
            : local < 8 ? Mathf.Lerp(0, 1.8f, Mathf.Clamp01(local - 4.5f))
            : local < 9 ? Mathf.Lerp(1.8f, 0, local - 8) : Mathf.Lerp(0, -1.8f, Mathf.Clamp01(local - 9));
        return new Vector3(owner % 2 == 0 ? x : -x, .95f, (owner - 3.5f) * .035f);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] internal sealed class Report
    {
        public bool passed, independentStates, productionAllocatorTested, rendererTested, sourceAdmissionTested;
        public int owners, ticks, fieldComparisons, ledgerComparisons, cpuRawComparisons;
        public long denseWorkspaceAndVelocityBytes, peakCandidateFieldBytes, peakEncodedOldNewBytes;
        public string note;
    }
}

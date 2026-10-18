using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Reproduce source-position aliasing in the same compiled solver used by native .221.
public static class FogReturnOwnershipCheck
{
    private const int Nx = 96, Ny = 80, Nz = 80;
    public static void Run() => Run(false);
    public static void RunCandidate() => Run(true);
    public static void RunRuntimeSpatial() => Run(true, true);
    public static void RunRuntimeWindow() => Run(true, true, true);
    private static void Run(bool candidate, bool runtime = false, bool window = false)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, window ? "artifacts/fog-spatial-window-224/verified" : runtime ? "artifacts/fog-spatial-ledger-223" : "artifacts/fog-return-ownership-222");
        Directory.CreateDirectory(output);
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            {
                var solver = UnityEngine.Object.Instantiate(candidate && !runtime
                    ? AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/OwnershipCandidate/SpatialLedger.compute") : assets.Solver);
                try
                {
                    solver.DisableKeyword("BUILDWORKS_FOG_COMPACT_GRID");
                    if (runtime && !solver.HasKernel("ClearSpatialLedger")) throw new NotSupportedException("Runtime spatial solver required.");
                    if (solver.HasKernel("ClearSpatialLedger")) solver.SetInt("_SpatialLedger", runtime ? 1 : 0);
                    int count = Nx * Ny * Nz;
                    Vector3 low = new Vector3(-3.6f, -.7f, -2.7f), high = new Vector3(3.6f, 4.4f, 2.7f);
                    int Index(int x, int y, int z) => x + Nx * (y + Ny * z);
                    Vector3 Point(int x, int y, int z) => low + Vector3.Scale(new Vector3((x + .5f) / Nx, (y + .5f) / Ny, (z + .5f) / Nz), high - low);
                    Vector3 source = Point(36, 26, 40);
                    var mass = new Color[count]; var origin = new Color[count];
                    int moved = Index(50, 30, 50);
                    mass[moved] = new Color(0, 0, 0, 4);
                    origin[moved] = new Color(source.x * 4, source.y * 4, source.z * 4, 0);
                    var smoke = Texture(mass); var origins = Texture(origin); var zero = Texture(new Color[count]);
                    var old = Texture(Enumerable.Repeat(new Color(0, 0, 0, .5f), count).ToArray());
                    var noise = new Texture3D(1, 1, 1, TextureFormat.RFloat, false);
                    noise.SetPixelData(new[] { .5f }, 0); noise.Apply(false, false);
                    var flow = new RenderTexture(Nx, Ny, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                        { dimension = TextureDimension.Tex3D, volumeDepth = Nz, enableRandomWrite = true, wrapMode = TextureWrapMode.Clamp };
                    int ledgerCells = candidate ? 16384 : 1024;
                    using (var away = new ComputeBuffer(ledgerCells, 4))
                    using (var peak = new ComputeBuffer(ledgerCells, 4))
                    using (var state = new ComputeBuffer(1, 4))
                    try
                    {
                        if (!flow.Create()) throw new InvalidOperationException("Ownership probe field unavailable.");
                        away.SetData(new uint[ledgerCells]); peak.SetData(new uint[ledgerCells]); state.SetData(new uint[1]);
                        solver.SetVector("_Minimum", low); solver.SetVector("_Maximum", high);
                        solver.SetVector("_RestMin", new Vector3(-2.4f, -.7f, -1.7f)); solver.SetVector("_RestMax", new Vector3(2.4f, 3.8f, 1.7f));
                        int ledger = solver.FindKernel("AccumulateLedger"), velocity = solver.FindKernel("AdvanceVelocity");
                        solver.SetTexture(ledger, "_ReadColour", smoke); solver.SetTexture(ledger, "_ReadOrigin", origins);
                        solver.SetBuffer(ledger, "_AwayMass", away); solver.SetBuffer(ledger, "_SurfaceState", state);
                        solver.Dispatch(ledger, 12, 10, 20);
                        var bins = new uint[ledgerCells]; away.GetData(bins);
                        if (bins.Count(v => v != 0) != 1 || bins.Sum(v => (long)v) != 1024) throw new InvalidOperationException("Synthetic displaced mass was not accounted exactly.");
                        solver.SetTexture(velocity, "_ReadColour", smoke); solver.SetTexture(velocity, "_ReadOrigin", origins);
                        solver.SetTexture(velocity, "_ReadVelocity", zero); solver.SetTexture(velocity, "_ReadSurface", old);
                        solver.SetTexture(velocity, "_Noise", noise); solver.SetTexture(velocity, "_Shape", noise);
                        solver.SetTexture(velocity, "_WriteVelocity", flow); solver.SetBuffer(velocity, "_AwayMass", away); solver.SetBuffer(velocity, "_PeakAway", peak);
                        solver.SetVector("_Actor", Vector3.one * 50); solver.SetVector("_ActorVelocity", Vector3.zero);
                        solver.SetVector("_Body", new Vector4(.3f, .95f, 0, 0)); solver.SetVector("_NoiseOffset", Vector3.zero);
                        solver.SetFloat("_Dt", 1f / 30); solver.SetFloat("_Emit", 0); solver.SetFloat("_Clock", 1); solver.SetFloat("_Seed", 37);
                        solver.Dispatch(velocity, 12, 10, 20);
                        var request = AsyncGPUReadback.Request(flow, 0, TextureFormat.RGBAFloat); request.WaitForCompletion();
                        if (request.hasError) throw new InvalidOperationException("Ownership probe readback failed.");
                        var values = new Color[count]; int offset = 0;
                        for (int layer = 0; layer < request.layerCount; ++layer)
                        {
                            var data = request.GetData<Color>(layer);
                            if (data.Length > values.Length - offset) throw new InvalidOperationException("Ownership probe readback exceeds extent.");
                            for (int i = 0; i < data.Length; ++i) values[offset++] = data[i];
                        }
                        if (offset != count) throw new InvalidOperationException("Ownership probe readback incomplete.");
                        float sourceDamage = values[Index(36, 26, 40)].a, otherX = values[Index(60, 26, 40)].a, otherZ = values[Index(36, 26, 60)].a;
                        if (sourceDamage != .5f || (candidate ? otherX < .45f || otherX > .46f : otherX != .5f) || otherZ < .45f || otherZ > .46f)
                            throw new InvalidOperationException("Expected YZ alias/control not reproduced: " + sourceDamage + "/" + otherX + "/" + otherZ);
                        var report = new Report { reproduced = !candidate, isolatedCandidatePassed = candidate, sourceDamage = sourceDamage, unrelatedXDamage = otherX,
                            unrelatedZControlDamage = otherZ, separatedXMetres = Point(60, 26, 40).x - source.x,
                            bin = Array.FindIndex(bins, v => v != 0), awayMass = 1024, graphics = SystemInfo.graphicsDeviceType.ToString() };
                        File.WriteAllText(Path.Combine(output, candidate ? "spatial-candidate.json" : "reproduction.json"), JsonUtility.ToJson(report, true));
                        Debug.Log("FOG_RETURN_OWNERSHIP_REPRODUCED " + JsonUtility.ToJson(report));
                    }
                    finally
                    {
                        flow.Release(); UnityEngine.Object.DestroyImmediate(flow);
                        foreach (var texture in new Texture[] { smoke, origins, zero, old, noise }) UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(solver); }
            }
            EditorApplication.Exit(0);
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static Texture3D Texture(Color[] data)
    {
        var result = new Texture3D(Nx, Ny, Nz, TextureFormat.RGBAHalf, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        result.SetPixels(data); result.Apply(false, false); return result;
    }
    [Serializable] private sealed class Report
    {
        public bool reproduced, isolatedCandidatePassed, fixedInRuntime = false, nativeGameTested = false, visualParityAccepted = false;
        public float sourceDamage, unrelatedXDamage, unrelatedZControlDamage, separatedXMetres;
        public int bin, awayMass; public string graphics;
    }
}

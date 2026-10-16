using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeAssetBuild
{
    public static void Run()
    { Build(ColorSpace.Linear, "fog-runtime-assets"); }

    public static void RunGammaReference()
    { Build(ColorSpace.Gamma, "fog-runtime-assets-gamma"); }

    private static void Build(ColorSpace colorSpace, string outputFolder)
    {
        ColorSpace previous = PlayerSettings.colorSpace;
        try
        {
            PlayerSettings.colorSpace = colorSpace;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (QualitySettings.activeColorSpace != colorSpace) throw new InvalidOperationException("Fog build color space did not change.");
            BuildCurrent(outputFolder);
        }
        finally { PlayerSettings.colorSpace = previous; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); }
    }

    private static void BuildCurrent(string outputFolder)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts", outputFolder);
        string source = Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime");
        Directory.CreateDirectory(output);
        foreach (string name in new[] { "AccumulatedSmoke.compute", "FogGhostVolume.shader", "TexturedGhost.shader", "GhostMatter.cginc", "FogVolumePair.cginc" })
            if (Hash(Path.Combine(source, name)) != Hash(Path.Combine(Application.dataPath, "BuildWorksFogRuntime", name)))
                throw new InvalidDataException("Approved v11 source differs: " + name);
        var build = new AssetBundleBuild {
            assetBundleName = "buildworks-fog-v11",
            assetNames = new[] { "Assets/BuildWorksFogRuntime/AccumulatedSmoke.compute", "Assets/BuildWorksFogRuntime/FogGhostVolume.shader", "Assets/BuildWorksFogRuntime/TexturedGhost.shader" }
        };
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new InvalidOperationException("Fog v11 asset build failed.");
        string path = Path.Combine(output, build.assetBundleName);
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Fog asset budget exceeded.");
        using (var assets = new BlueprintFogAssets(File.ReadAllBytes(path)))
        {
            CheckLedger(assets.Solver);
            var surface = new Material(assets.Surface); var volume = new Material(assets.Volume);
            try
            {
                if (!surface.HasProperty("_MainTex") || !surface.HasProperty("_BumpMap") || !volume.HasProperty("_Wake"))
                    throw new InvalidDataException("Fog v11 texture bindings missing.");
            }
            finally { UnityEngine.Object.DestroyImmediate(surface); UnityEngine.Object.DestroyImmediate(volume); }
        }
        using (var again = new BlueprintFogAssets(File.ReadAllBytes(path))) CheckLedger(again.Solver);
        bool rejected = false;
        try { using (var invalid = new BlueprintFogAssets(Array.Empty<byte>())) { } }
        catch (InvalidDataException) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("Empty resource was accepted.");
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
            passed = true, unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(),
            sha256 = Hash(path), bytes = new FileInfo(path).Length, assets = 3, kernels = 7,
            ledgerResetAndReload = true, emptyRejected = rejected, gameHostTested = false
        }, true));
        Debug.Log("BuildWorks approved fog v11 bundle: build/load/7 kernels/GPU ledger/reload/empty rejection PASSED");
    }

    internal static void CheckLedger(ComputeShader solver)
    {
        using (var away = new ComputeBuffer(1024, sizeof(uint)))
        using (var peak = new ComputeBuffer(1024, sizeof(uint)))
        using (var state = new ComputeBuffer(1, sizeof(uint)))
        {
            uint[] values = Enumerable.Repeat(123u, 1024).ToArray();
            away.SetData(values); peak.SetData(values); state.SetData(new[] { 123u });
            int kernel = solver.FindKernel("ClearLedger");
            solver.SetBuffer(kernel, "_AwayMass", away); solver.SetBuffer(kernel, "_PeakAway", peak);
            solver.SetBuffer(kernel, "_SurfaceState", state); solver.SetInt("_ResetLedger", 1);
            solver.Dispatch(kernel, 16, 1, 1);
            away.GetData(values); if (values.Any(value => value != 0)) throw new InvalidDataException("Away ledger did not clear.");
            peak.GetData(values); if (values.Any(value => value != 0)) throw new InvalidDataException("Peak ledger did not clear.");
            state.GetData(values, 0, 0, 1); if (values[0] != 0) throw new InvalidDataException("Surface state did not clear.");
        }
    }

    private static string Hash(string path)
    { using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }

    [Serializable] private sealed class Report
    { public bool passed, ledgerResetAndReload, emptyRejected, gameHostTested; public string unity, graphics, sha256, colorSpace; public long bytes; public int assets, kernels; }
}

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

    public static void RunCandidate()
    { Build(ColorSpace.Linear, "fog-runtime-assets-candidate"); }

    public static void RunWindowGammaReference()
    { Build(ColorSpace.Gamma, "fog-window-gamma-225"); }

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
        foreach (string name in BlueprintFogAssetLayout.Sources)
            if (Hash(Path.Combine(source, name)) != Hash(Path.Combine(Application.dataPath, "BuildWorksFogRuntime", name)))
                throw new InvalidDataException("Approved v11 source differs: " + name);
        CheckShaders(AssetDatabase.LoadAssetAtPath<Shader>("Assets/BuildWorksFogRuntime/TexturedGhost.shader"),
            AssetDatabase.LoadAssetAtPath<Shader>("Assets/BuildWorksFogRuntime/FogGhostVolume.shader"));
        var build = new AssetBundleBuild {
            assetBundleName = "buildworks-fog-v11",
            assetNames = BlueprintFogAssetLayout.Sources.Where(name => name.EndsWith(".compute", StringComparison.Ordinal) || name.EndsWith(".shader", StringComparison.Ordinal))
                .Select(name => "Assets/BuildWorksFogRuntime/" + name).ToArray()
        };
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new InvalidOperationException("Fog v11 asset build failed.");
        string path = Path.Combine(output, build.assetBundleName);
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Fog asset budget exceeded.");
        using (var assets = new BlueprintFogAssets(File.ReadAllBytes(path)))
        {
            CheckShaders(assets.Surface, assets.Volume);
            CheckLedger(assets.Solver);
            CheckArena(assets);
            var surface = new Material(assets.Surface); var volume = new Material(assets.Volume);
            try
            {
                if (!surface.HasProperty("_MainTex") || !surface.HasProperty("_BumpMap") || !volume.HasProperty("_Wake"))
                    throw new InvalidDataException("Fog v11 texture bindings missing.");
            }
            finally { UnityEngine.Object.DestroyImmediate(surface); UnityEngine.Object.DestroyImmediate(volume); }
        }
        using (var again = new BlueprintFogAssets(File.ReadAllBytes(path))) { CheckLedger(again.Solver); CheckArena(again); }
        bool rejected = false;
        try { using (var invalid = new BlueprintFogAssets(Array.Empty<byte>())) { } }
        catch (InvalidDataException) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("Empty resource was accepted.");
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
            passed = true, unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(),
            sha256 = Hash(path), bytes = new FileInfo(path).Length, assets = 5, kernels = 28, arenaPublicationAndReload = true,
            ledgerResetAndReload = true, emptyRejected = rejected, gameHostTested = false, shaderMessagesChecked = true,
            sources = BlueprintFogAssetLayout.Sources
                .Select(name => new Source { name = name, sha256 = Hash(Path.Combine(source, name)) }).ToArray()
        }, true));
        Debug.Log("BuildWorks fog v11 bundle: build/load/28 kernels/GPU ledger/arena publication/reload/empty rejection PASSED");
    }

    private static void CheckArena(BlueprintFogAssets assets)
    {
        if (!assets.Arena || !assets.Metadata || assets.AssetCount != 5 || assets.KernelCount != 28)
            throw new InvalidDataException("Fog bundle did not load the runtime arena programs.");
        using (var arena = new BlueprintFogArena(assets.Arena, assets.Metadata, 1, 1))
        {
            var token = new BlueprintFogSnapshotState.Submission((1L << 33) + 7, (1L << 34) + 9, 3, 2, 1);
            arena.PublishMetadata(0, token, new Vector3(1, 2, 3), new Vector2(.3f, .95f), .14f, 1, mapBase: BlueprintFogArena.MapWords);
            var mapBases = new uint[1]; arena.Buffers["_PublishedMapBases"].GetData(mapBases);
            if (mapBases[0] != BlueprintFogArena.MapWords) throw new InvalidDataException("Fog GPU map publication differs from its committed bank.");
            bool invalidMapRejected = false;
            try { arena.PublishMetadata(0, token, Vector3.zero, Vector2.one, 0, 0, mapBase: BlueprintFogArena.MapWords * 2); }
            catch (ArgumentException) { invalidMapRejected = true; }
            if (!invalidMapRejected) throw new InvalidDataException("Fog publication accepted another slot's map.");
            var values = new uint[16]; arena.Buffers["_PublishedMetadata"].GetData(values);
            if (values[7] != 3 || values[8] != 7 || values[9] != 2 || values[10] != 9 || values[11] != 4 || values[12] != 3 || values[13] != 0 || values[14] != 2 || values[15] != 1)
                throw new InvalidDataException("Bundled fog arena did not publish exact owner metadata.");
            var rejected = new uint[16]; rejected[2] = 8; arena.Buffers["_Control"].SetData(rejected);
            arena.PublishMetadata(0, token, Vector3.zero, Vector2.one, 0, 0, 0);
            arena.Buffers["_PublishedMapBases"].GetData(mapBases);
            if (mapBases[0] != BlueprintFogArena.MapWords) throw new InvalidDataException("Rejected publication switched the confirmed map bank.");
            var retained = new uint[16]; arena.Buffers["_PublishedMetadata"].GetData(retained);
            if (!values.SequenceEqual(retained)) throw new InvalidDataException("Rejected arena publication changed the confirmed metadata.");
        }
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

    internal static void CheckShaders(params Shader[] shaders)
    {
        foreach (var shader in shaders)
        {
            if (!shader || !shader.isSupported) throw new InvalidDataException("Fog shader missing or unsupported.");
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if (errors.Length != 0) throw new InvalidDataException(shader.name + ": " + string.Join("; ", errors.Select(m => m.message)));
        }
    }

    private static string Hash(string path)
    { using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }

    [Serializable] private sealed class Report
    { public bool passed, ledgerResetAndReload, emptyRejected, gameHostTested, shaderMessagesChecked, arenaPublicationAndReload; public string unity, graphics, sha256, colorSpace; public long bytes; public int assets, kernels; public Source[] sources; }
    [Serializable] private sealed class Source { public string name, sha256; }
}

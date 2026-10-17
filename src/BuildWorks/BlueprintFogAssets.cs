using System;
using System.IO;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed class BlueprintFogAssets : IDisposable
    {
        internal const string ResourceName = "BuildWorks.FogGhostV11";
        internal static readonly string[] Kernels = { "Clear", "AdvanceVelocity", "AdvanceMatter", "CorrectMatter", "ClearLedger", "AccumulateLedger", "UpdateSurface", "PackVolume" };
        private AssetBundle bundle;
        internal Texture3D Empty { get; private set; }
        internal Shader Surface { get; private set; }
        internal Shader Volume { get; private set; }
        internal ComputeShader Solver { get; private set; }
        internal ComputeShader Arena { get; private set; }
        internal ComputeShader Metadata { get; private set; }
        internal int AssetCount => Arena ? BlueprintFogAssetLayout.Current.Length : BlueprintFogAssetLayout.Legacy.Length;
        internal int KernelCount => Kernels.Length + (Arena ? BlueprintFogArena.KernelNames.Length + 2 : 0);

        internal static BlueprintFogAssets LoadEmbedded()
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new NotSupportedException("BuildWorks embedded fog shaders require Valheim's Linear color space.");
            using (var stream = typeof(BlueprintFogAssets).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null || stream.Length <= 0 || stream.Length > 16 * 1024 * 1024)
                    throw new InvalidDataException("Fog v11 resource missing or outside its size limit.");
                using (var bytes = new MemoryStream())
                { stream.CopyTo(bytes); return new BlueprintFogAssets(bytes.ToArray()); }
            }
        }

        internal BlueprintFogAssets(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 16 * 1024 * 1024)
                throw new InvalidDataException("Fog v11 bundle size invalid.");
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports3DTextures)
                throw new NotSupportedException("Fog v11 requires compute shaders and 3D textures.");
            try
            {
                bundle = AssetBundle.LoadFromMemory(bytes);
                if (!bundle) throw new InvalidDataException("Fog v11 bundle could not be loaded.");
                bool hasArena = BlueprintFogAssetLayout.HasArena(bundle.GetAllAssetNames());
                var expected = BlueprintFogAssetLayout.Legacy;
                Surface = bundle.LoadAsset<Shader>(expected[2]); Volume = bundle.LoadAsset<Shader>(expected[1]);
                Solver = bundle.LoadAsset<ComputeShader>(expected[0]);
                if (!Surface || Surface.name != "BuildWorks/Fog/TexturedGhost" || !Surface.isSupported ||
                    !Volume || Volume.name != "Hidden/BuildWorks/FogGhostVolumeRuntime" || !Volume.isSupported || !Solver)
                    throw new NotSupportedException("Fog v11 shaders are unavailable on this graphics device.");
                foreach (string kernel in Kernels)
                {
                    CheckKernel(Solver, kernel, kernel == "ClearLedger" ? 64u : 8u, kernel == "ClearLedger" ? 1u : 8u, kernel == "ClearLedger" ? 1u : 4u);
                }
                if (hasArena)
                {
                    Arena = bundle.LoadAsset<ComputeShader>(BlueprintFogAssetLayout.Prefix + "fogarena.compute");
                    Metadata = bundle.LoadAsset<ComputeShader>(BlueprintFogAssetLayout.Prefix + "fogsnapshotmetadata.compute");
                    foreach (string kernel in BlueprintFogArena.KernelNames)
                    {
                        bool field = kernel == "IndexFields" || kernel == "PackFields" || kernel == "CommitVelocity" || kernel == "PublishFields" || kernel == "RestoreFields";
                        bool serial = kernel == "BeginStep" || kernel == "ReserveBlocks" || kernel == "CheckPacking" || kernel == "FinishStep";
                        CheckKernel(Arena, kernel, field ? 4u : serial ? 1u : 64u, field ? 4u : 1u, field ? 4u : 1u);
                    }
                    CheckKernel(Metadata, "InitMetadata", 64, 1, 1); CheckKernel(Metadata, "PublishMetadata", 1, 1, 1);
                }
                Empty = new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false) {
                    name = "BuildWorks empty fog field", wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
                Empty.SetPixels(new[] { Color.clear }); Empty.Apply(false, true);
            }
            catch { Dispose(); throw; }
        }

        private static void CheckKernel(ComputeShader program, string name, uint expectedX, uint expectedY, uint expectedZ)
        {
            if (!program || !program.HasKernel(name)) throw new InvalidDataException("Fog v11 kernel missing: " + name);
            program.GetKernelThreadGroupSizes(program.FindKernel(name), out uint x, out uint y, out uint z);
            if (x != expectedX || y != expectedY || z != expectedZ) throw new InvalidDataException("Fog v11 kernel layout differs: " + name);
        }

        public void Dispose()
        {
            if (Empty) UnityEngine.Object.DestroyImmediate(Empty); Empty = null;
            if (bundle) bundle.Unload(true);
            bundle = null; Surface = null; Volume = null; Solver = null; Arena = null; Metadata = null;
        }
    }
}

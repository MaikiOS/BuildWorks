using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed class BlueprintFogAssets : IDisposable
    {
        internal const string ResourceName = "BuildWorks.FogGhostV11";
        internal static readonly string[] Kernels = { "Clear", "AdvanceVelocity", "AdvanceMatter", "CorrectMatter", "ClearLedger", "AccumulateLedger", "UpdateSurface", "PackVolume" };
        private AssetBundle bundle;
        internal Shader Surface { get; private set; }
        internal Shader Volume { get; private set; }
        internal ComputeShader Solver { get; private set; }

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
                var expected = new[] { "assets/buildworksfogruntime/accumulatedsmoke.compute", "assets/buildworksfogruntime/fogghostvolume.shader", "assets/buildworksfogruntime/texturedghost.shader" };
                if (!bundle.GetAllAssetNames().OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(expected))
                    throw new InvalidDataException("Fog v11 bundle contains unexpected assets.");
                Surface = bundle.LoadAsset<Shader>(expected[2]); Volume = bundle.LoadAsset<Shader>(expected[1]);
                Solver = bundle.LoadAsset<ComputeShader>(expected[0]);
                if (!Surface || Surface.name != "BuildWorks/Fog/TexturedGhost" || !Surface.isSupported ||
                    !Volume || Volume.name != "Hidden/BuildWorks/FogGhostVolumeRuntime" || !Volume.isSupported || !Solver)
                    throw new NotSupportedException("Fog v11 shaders are unavailable on this graphics device.");
                foreach (string kernel in Kernels)
                {
                    int index = Solver.FindKernel(kernel);
                    Solver.GetKernelThreadGroupSizes(index, out uint x, out uint y, out uint z);
                    if (kernel == "ClearLedger" ? x != 64 || y != 1 || z != 1 : x != 8 || y != 8 || z != 4)
                        throw new InvalidDataException("Fog v11 kernel layout differs: " + kernel);
                }
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (bundle) bundle.Unload(true);
            bundle = null; Surface = null; Volume = null; Solver = null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // GPU resource/dispatch core. The channel owner drains its readbacks before disposing this arena.
    internal sealed class BlueprintFogArena : IDisposable
    {
        internal const int MapWords = 28800;
        internal const long MaximumFieldBytes = 98304000;
        internal static readonly string[] KernelNames = { "InitArena", "BeginStep", "StageLedger", "IndexFields", "ValidateCurrent", "ReserveBlocks", "ResolveBlocks", "PackFields", "CheckPacking", "CommitVelocity", "CommitLedger", "PublishFields", "CommitMaps", "FinishStep", "RestoreFields", "ReleaseCurrent", "ClearOwnerMaps", "PackStaticSource" };
        private readonly Dictionary<string, int> kernels = new Dictionary<string, int>();
        private readonly Dictionary<string, ComputeBuffer> buffers = new Dictionary<string, ComputeBuffer>();
        private readonly ComputeShader shader, metadataShader;
        private readonly int initMetadata, publishMetadata;
        private readonly RenderTexture[] reads = new RenderTexture[3];
        private bool disposed;
        private readonly int ownerCount;
        internal IReadOnlyDictionary<string, ComputeBuffer> Buffers => buffers;
        internal ComputeShader Program => shader;
        internal BlueprintFogScratch Scratch { get; }
        internal RenderTexture Smoke { get; }
        internal RenderTexture Surface { get; }
        // Includes the committed velocity/ledger reservation that the channel owner allocates separately.
        internal long ReservedFieldBytes { get; }
        internal long RenderBytes { get; }
        internal int BlockCapacity { get; }

        internal BlueprintFogArena(ComputeShader arenaProgram, ComputeShader metadataProgram, int owners, int capacity)
        {
            if (!arenaProgram || !metadataProgram) throw new ArgumentException("Fog arena programs missing.");
            ReservedFieldBytes = BlueprintFogSnapshotState.EstimateFieldBytes(owners, capacity); ownerCount = owners; BlockCapacity = capacity;
            RenderBytes = checked(98L * 82 * 82 * owners * 16);
            if (ReservedFieldBytes > MaximumFieldBytes || RenderBytes > BlueprintFogAtlas.MaximumBytes || Math.Max(98L, 82L * owners) > SystemInfo.maxTexture3DSize)
                throw new ArgumentException("Fog arena exceeds its field or render reservation.");
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports3DTextures) throw new NotSupportedException("Fog arena requires compute and 3D textures.");
            try
            {
                shader = UnityEngine.Object.Instantiate(arenaProgram);
                if (!shader) throw new InvalidOperationException("Arena compute missing.");
                foreach (string name in KernelNames) kernels.Add(name, shader.FindKernel(name));
                Add("_Maps", owners * 2 * MapWords, 4); Add("_Free", capacity, 4); Add("_Control", 16, 4); Add("_Receipts", owners * 16, 4); Add("_Payload", capacity * 64, 8);
                Add("_StageAway", 1024, 4); Add("_StagePeak", 1024, 4); Add("_StageState", 1, 4);
                metadataShader = UnityEngine.Object.Instantiate(metadataProgram);
                if (!metadataShader) throw new InvalidOperationException("Metadata compute missing.");
                initMetadata = metadataShader.FindKernel("InitMetadata"); publishMetadata = metadataShader.FindKernel("PublishMetadata");
                Add("_PublishedMetadata", owners * 16, 4);
                metadataShader.SetInt("_MetadataWords", owners * 16);
                metadataShader.SetBuffer(initMetadata, "_PublishedMetadata", buffers["_PublishedMetadata"]);
                metadataShader.SetBuffer(publishMetadata, "_PublishedMetadata", buffers["_PublishedMetadata"]);
                metadataShader.SetBuffer(publishMetadata, "_Control", buffers["_Control"]);
                metadataShader.Dispatch(initMetadata, (owners * 16 + 63) / 64, 1, 1);
                shader.SetInts("_Grid", 96, 80, 80); shader.SetInts("_Blocks", 24, 20, 20);
                shader.SetInt("_Capacity", capacity); shader.SetInt("_MapWords", MapWords); shader.SetInt("_TotalMapWords", owners * 2 * MapWords); shader.SetInt("_ReceiptWords", owners * 16);
                Bind("InitArena", "_Maps", "_Free", "_Control", "_Receipts"); Bind("BeginStep", "_Control");
                Bind("StageLedger", "_StageAway", "_StagePeak", "_StageState"); Bind("CommitLedger", "_StageAway", "_StagePeak", "_StageState", "_Control");
                Bind("IndexFields", "_Maps", "_Control"); Bind("ValidateCurrent", "_Maps", "_Control"); Bind("ReserveBlocks", "_Control");
                Bind("ResolveBlocks", "_Maps", "_Free", "_Control"); Bind("PackFields", "_Maps", "_Control", "_Payload"); Bind("CheckPacking", "_Control");
                Bind("CommitVelocity", "_Control"); Bind("PublishFields", "_Control"); Bind("CommitMaps", "_Maps", "_Free", "_Control"); Bind("FinishStep", "_Control", "_Receipts");
                Bind("RestoreFields", "_Maps", "_Payload", "_Control");
                Bind("ReleaseCurrent", "_Maps", "_Free", "_Control"); Bind("ClearOwnerMaps", "_Maps");
                Scratch = new BlueprintFogScratch(new Vector3Int(96, 80, 80), includeReadFields: true);
                reads[0] = Scratch.ReadColour; reads[1] = Scratch.ReadOrigin; reads[2] = Scratch.ReadSurface;
                foreach (string name in new[] { "IndexFields", "PackFields", "PublishFields" })
                {
                    shader.SetTexture(kernels[name], "_ColourIn", reads[0]); shader.SetTexture(kernels[name], "_SurfaceIn", reads[2]);
                    if (name != "PublishFields") shader.SetTexture(kernels[name], "_OriginIn", reads[1]);
                }
                for (int f = 0; f < 3; ++f) shader.SetTexture(kernels["RestoreFields"], new[] { "_ColourOut", "_OriginOut", "_SurfaceOut" }[f], reads[f]);
                shader.SetTexture(kernels["CommitVelocity"], "_NextVelocity", Scratch.Velocity);
                Smoke = Atlas(owners); Surface = Atlas(owners); shader.SetTexture(kernels["PublishFields"], "_PublishedSmoke", Smoke); shader.SetTexture(kernels["PublishFields"], "_PublishedSurface", Surface);
                Dispatch("InitArena", (Math.Max(owners * 2 * MapWords, capacity) + 63) / 64);
            }
            catch { Dispose(); throw; }
        }
        private void Add(string name, int count, int stride) => buffers.Add(name, new ComputeBuffer(count, stride));
        private void Bind(string name, params string[] names) { foreach (string buffer in names) shader.SetBuffer(kernels[name], buffer, buffers[buffer]); }
        internal int Kernel(string name) => kernels[name];
        internal void Dispatch(string name, int x = 1, int y = 1, int z = 1)
        { if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogArena)); shader.Dispatch(kernels[name], x, y, z); }
        private static RenderTexture Atlas(int owners)
        {
            var texture = new RenderTexture(98, 82, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) {
                dimension = TextureDimension.Tex3D, volumeDepth = checked(82 * owners), enableRandomWrite = true,
                filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            if (!texture.Create()) { UnityEngine.Object.DestroyImmediate(texture); throw new InvalidOperationException("Fog arena atlas allocation failed."); }
            return texture;
        }
        internal void PublishMetadata(int slot, BlueprintFogSnapshotState.Submission token, Vector3 actor, Vector2 capsule, float clock, float weight, uint flags = 3)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogArena));
            if (slot < 0 || slot >= ownerCount) throw new ArgumentOutOfRangeException(nameof(slot));
            metadataShader.SetInt("_Slot", slot); metadataShader.SetInt("_Tick", token.Tick); metadataShader.SetInt("_EmissionRevision", token.EmissionRevision); metadataShader.SetInt("_PublishFlags", (int)flags);
            metadataShader.SetInts("_OwnerBits", unchecked((int)token.Owner), (int)(token.Owner >> 32));
            metadataShader.SetInts("_GenerationBits", unchecked((int)token.Generation), (int)(token.Generation >> 32));
            metadataShader.SetInts("_SequenceBits", unchecked((int)token.Sequence), (int)(token.Sequence >> 32));
            metadataShader.SetVector("_ActorRadius", new Vector4(actor.x, actor.y, actor.z, capsule.x));
            metadataShader.SetVector("_ShapeClock", new Vector4(capsule.y, clock, weight, 0));
            metadataShader.Dispatch(publishMetadata, 1, 1, 1);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var buffer in buffers.Values) buffer.Release(); buffers.Clear();
            foreach (var texture in new[] { Smoke, Surface }) if (texture) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            Scratch?.Dispose();
            if (shader) UnityEngine.Object.DestroyImmediate(shader); if (metadataShader) UnityEngine.Object.DestroyImmediate(metadataShader);
        }
    }
}

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Test-only codec. Synchronous count readbacks and allocations are measurements, not a shipping allocator.
internal sealed class FogSparseStorageProbe : IDisposable
{
    internal sealed class Encoded : IDisposable
    {
        internal readonly Vector3Int Grid;
        internal readonly int LiveBlocks;
        internal ComputeBuffer Map, Packed;
        internal long Bytes => Map == null ? 0 : (long)Map.count * Map.stride + (Packed == null ? 0 : (long)Packed.count * Packed.stride);
        internal Encoded(Vector3Int grid, int live, ComputeBuffer map, ComputeBuffer packed)
        { Grid = grid; LiveBlocks = live; Map = map; Packed = packed; }
        public void Dispose() { Map?.Release(); Packed?.Release(); Map = Packed = null; }
    }

    private ComputeShader shader;
    private ComputeBuffer count, flags, empty;
    private readonly uint[] zero = { 0 };
    private readonly int index, pack, unpack, clear, compare, words;
    internal long CommonBufferBytes => 16;

    internal FogSparseStorageProbe()
    {
        try
        {
            ComputeShader source = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/FogSparseStorageProbe.compute");
            if (!source) throw new InvalidOperationException("Sparse measurement shader is missing.");
            shader = UnityEngine.Object.Instantiate(source); shader.hideFlags = HideFlags.HideAndDontSave;
            index = Kernel("IndexBlocks"); pack = Kernel("PackBlocks"); unpack = Kernel("UnpackBlocks");
            clear = Kernel("ClearDense"); compare = Kernel("CompareDense"); words = Kernel("CompareWords", true);
            count = new ComputeBuffer(1, 4); flags = new ComputeBuffer(1, 4); empty = new ComputeBuffer(1, 8);
            empty.SetData(new ulong[1]);
            foreach (int kernel in new[] { index, pack, unpack, compare, words }) shader.SetBuffer(kernel, "_Flags", flags);
            shader.SetBuffer(index, "_Count", count);
        }
        catch { Dispose(); throw; }
    }

    private int Kernel(string name, bool linear = false)
    {
        int kernel = shader.FindKernel(name);
        shader.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
        if (linear ? x != 64 || y != 1 || z != 1 : x != 4 || y != 4 || z != 4)
            throw new InvalidOperationException("Sparse measurement kernel layout differs: " + name);
        return kernel;
    }

    internal Encoded Encode(RenderTexture input, long byteBudget)
    {
        Vector3Int grid = Grid(input);
        int blocks = checked(grid.x / 4 * (grid.y / 4) * (grid.z / 4));
        if (byteBudget < blocks * 4L || byteBudget > 98304000) throw new ArgumentException("Invalid sparse measurement byte budget.");
        ComputeBuffer map = null, packed = null;
        try
        {
            map = new ComputeBuffer(blocks, 4); count.SetData(zero); flags.SetData(zero);
            shader.SetTexture(index, "_Dense", input); shader.SetBuffer(index, "_Map", map); Dispatch(index, grid);
            uint live = Read(count); RequireClean();
            if (live > blocks) throw new InvalidOperationException("Sparse block count exceeds its grid.");
            long bytes = blocks * 4L + live * 64L * 8;
            if (bytes > byteBudget) throw new InvalidOperationException("Sparse measurement exceeds its byte budget; previous state is retained.");
            if (live != 0)
            {
                packed = new ComputeBuffer(checked((int)live * 64), 8);
                shader.SetInt("_Capacity", (int)live); shader.SetTexture(pack, "_Dense", input);
                shader.SetBuffer(pack, "_Map", map); shader.SetBuffer(pack, "_Packed", packed); Dispatch(pack, grid);
                RequireClean();
            }
            return new Encoded(grid, (int)live, map, packed);
        }
        catch { map?.Release(); packed?.Release(); throw; }
    }

    internal void Restore(Encoded state, RenderTexture output)
    {
        Vector3Int grid = Grid(output);
        if (state == null || state.Map == null || state.Grid != grid || (state.LiveBlocks != 0 && state.Packed == null))
            throw new ArgumentException("Sparse measurement state is missing or has another grid.");
        flags.SetData(zero);
        shader.SetTexture(clear, "_Restored", output); Dispatch(clear, grid);
        shader.SetInt("_Capacity", state.LiveBlocks); shader.SetBuffer(unpack, "_Map", state.Map);
        shader.SetBuffer(unpack, "_Packed", state.Packed ?? empty); shader.SetTexture(unpack, "_Restored", output); Dispatch(unpack, grid);
        RequireClean();
    }

    internal void BeginComparison() { flags.SetData(zero); }
    internal void Compare(RenderTexture actual, RenderTexture expected)
    {
        Vector3Int grid = Grid(actual);
        if (grid != Grid(expected)) throw new ArgumentException("Sparse comparison grid differs.");
        shader.SetTexture(compare, "_Dense", actual); shader.SetTexture(compare, "_Expected", expected); Dispatch(compare, grid);
    }
    internal void Compare(ComputeBuffer actual, ComputeBuffer expected)
    {
        if (actual == null || expected == null || actual.stride != 4 || expected.stride != 4 || actual.count != expected.count)
            throw new ArgumentException("Sparse comparison ledger differs.");
        shader.SetInt("_WordCount", actual.count); shader.SetBuffer(words, "_WordsA", actual);
        shader.SetBuffer(words, "_WordsB", expected); shader.Dispatch(words, (actual.count + 63) / 64, 1, 1);
    }
    internal void RequireClean()
    {
        uint error = Read(flags);
        if (error != 0) throw new InvalidOperationException("Sparse measurement failed, flags=" + error + ".");
    }

    private static uint Read(ComputeBuffer buffer)
    {
        var request = AsyncGPUReadback.Request(buffer); request.WaitForCompletion();
        if (request.hasError) throw new InvalidOperationException("Sparse measurement readback failed.");
        var data = request.GetData<uint>();
        if (data.Length != 1) throw new InvalidOperationException("Incomplete sparse measurement counter.");
        return data[0];
    }
    private void Dispatch(int kernel, Vector3Int grid)
    { shader.SetInts("_Blocks", grid.x / 4, grid.y / 4, grid.z / 4); shader.Dispatch(kernel, grid.x / 4, grid.y / 4, grid.z / 4); }
    private static Vector3Int Grid(RenderTexture texture)
    {
        if (!texture || !texture.IsCreated() || texture.dimension != TextureDimension.Tex3D || texture.format != RenderTextureFormat.ARGBHalf ||
            texture.width < 4 || texture.height < 4 || texture.volumeDepth < 4 || texture.width > 256 || texture.height > 256 ||
            texture.volumeDepth > 256 || texture.width % 4 != 0 || texture.height % 4 != 0 ||
            texture.volumeDepth % 4 != 0 || (long)texture.width * texture.height * texture.volumeDepth > 96L * 80 * 80)
            throw new ArgumentException("Sparse measurement requires a bounded RGBAHalf3D grid divisible by4.");
        return new Vector3Int(texture.width, texture.height, texture.volumeDepth);
    }
    public void Dispose()
    {
        count?.Release(); flags?.Release(); empty?.Release(); count = flags = empty = null;
        if (shader) UnityEngine.Object.DestroyImmediate(shader); shader = null;
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // Borrowed only by serial advances on Unity's ordinary GPU queue, with an exact matching grid.
    internal sealed class BlueprintFogScratch : IDisposable
    {
        private readonly RenderTexture[] fields;
        internal Vector3Int GridSize { get; }
        internal long FieldBytes => BlueprintFogSimulation.FieldVoxels(GridSize) * fields.Length * 8;
        internal int Users;
        internal RenderTexture Colour => fields[0];
        internal RenderTexture Velocity => fields[1];
        internal RenderTexture Origin => fields[2];
        internal RenderTexture Surface => fields[3];
        internal RenderTexture PredictedColour => fields[4];
        internal RenderTexture PredictedOrigin => fields[5];
        internal RenderTexture ReadColour => fields.Length == 9 ? fields[6] : null;
        internal RenderTexture ReadOrigin => fields.Length == 9 ? fields[7] : null;
        internal RenderTexture ReadSurface => fields.Length == 9 ? fields[8] : null;

        internal BlueprintFogScratch(Vector3Int grid, bool includeReadFields = false)
        {
            BlueprintFogSimulation.FieldVoxels(grid); GridSize = grid;
            fields = new RenderTexture[includeReadFields ? 9 : 6];
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ||
                (SystemInfo.copyTextureSupport & CopyTextureSupport.Copy3D) == 0)
                throw new NotSupportedException("Shared fog scratch requires half-float 3D textures and native copying.");
            try
            {
                for (int i = 0; i < fields.Length; ++i)
                    fields[i] = BlueprintFogSimulation.AllocateField(grid, "Shared scratch " + i);
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            for (int i = 0; i < fields.Length; ++i)
            {
                if (fields[i]) { fields[i].Release(); UnityEngine.Object.DestroyImmediate(fields[i]); }
                fields[i] = null;
            }
        }
    }
}

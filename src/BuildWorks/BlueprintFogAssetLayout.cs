using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OstrixMods.BuildWorks
{
    // Exact bundle allowlist, also checked without loading Unity or allocating GPU resources.
    internal static class BlueprintFogAssetLayout
    {
        internal const string Prefix = "assets/buildworksfogruntime/";
        internal static readonly string[] Legacy = { Prefix + "accumulatedsmoke.compute", Prefix + "fogghostvolume.shader", Prefix + "texturedghost.shader" };
        internal static readonly string[] Current = Legacy.Concat(new[] { Prefix + "fogarena.compute", Prefix + "fogsnapshotmetadata.compute" }).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        internal static readonly string[] Sources = { "AccumulatedSmoke.compute", "FogGhostVolume.shader", "TexturedGhost.shader", "GhostMatter.cginc", "FogVolumePair.cginc", "FogVolumeAtlas.cginc", "FogPublished.cginc", "FogArena.compute", "FogSnapshotMetadata.compute" };

        internal static bool HasArena(IEnumerable<string> names)
        {
            if (names == null) throw new InvalidDataException("Fog v11 bundle asset list missing.");
            var actual = names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (actual.SequenceEqual(Current)) return true;
            if (actual.SequenceEqual(Legacy)) return false;
            throw new InvalidDataException("Fog v11 bundle contains missing or unexpected assets.");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class FogAssetChecks
    {
        internal static void Run()
        {
            var legacy = BlueprintFogAssetLayout.Legacy;
            var current = BlueprintFogAssetLayout.Current;
            Require(!BlueprintFogAssetLayout.HasArena(legacy.Reverse()), "Legacy bundle rejected or advertised an arena.");
            Require(BlueprintFogAssetLayout.HasArena(current.Reverse()), "Current bundle depends on asset order.");
            Reject(null); Reject(Array.Empty<string>());
            foreach (var list in new[] { legacy, current })
            {
                for (int i = 0; i < list.Length; ++i)
                {
                    Reject(list.Where((_, index) => index != i).ToArray());
                    var duplicate = list.ToArray(); duplicate[i] = list[(i + 1) % list.Length]; Reject(duplicate);
                    var wrongCase = list.ToArray(); wrongCase[i] = list[i].ToUpperInvariant(); Reject(wrongCase);
                }
                Reject(list.Concat(new[] { "assets/unexpected.compute" }).ToArray());
                Reject(list.Concat(new[] { list[0] }).ToArray());
                Reject(list.Concat(new string[] { null }).ToArray());
            }
            Require(BlueprintFogAssetLayout.Sources.Length == 9 && BlueprintFogAssetLayout.Sources.Distinct().Count() == 9, "Source manifest changed.");
        }
        private static void Reject(string[] names)
        {
            try { BlueprintFogAssetLayout.HasArena(names); }
            catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Invalid or partial fog bundle accepted.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}

using System;
using System.IO;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class ForeignZdoChecks
    {
        internal static void Run()
        {
            byte[] all = Package(writer => {
                writer.Write(4063);
                writer.Write((byte)1); writer.Write(1); writer.Write(1.5f);
                writer.Write((byte)1); writer.Write(2); writer.Write(2f); writer.Write(3f); writer.Write(4f);
                writer.Write((byte)1); writer.Write(3); writer.Write(0f); writer.Write(0f); writer.Write(0f); writer.Write(1f);
                writer.Write((byte)1); writer.Write(4); writer.Write(42);
                writer.Write((byte)1); writer.Write(5); writer.Write(long.MaxValue);
                writer.Write((byte)1); writer.Write(6); writer.Write("Надпись\0🪵");
                writer.Write((byte)1); writer.Write(7); writer.Write(3); writer.Write(new byte[] { 1, 2, 3 });
                writer.Write((byte)2); writer.Write(17); writer.Write(true); writer.Write(false); writer.Write((byte)1);
            });
            Require(BlueprintForeignZdo.TryRead(Convert.ToBase64String(all), out var data) && data.Count == 7 &&
                data.TryGet<float>(1, out var f) && f == 1.5f && data.TryGet<Point3>(2, out var v) && v.Z == 4 &&
                data.TryGet<Rotation3>(3, out var q) && q.W == 1 && data.TryGet<int>(4, out var i) && i == 42 &&
                data.TryGet<long>(5, out var l) && l == long.MaxValue && data.TryGet<string>(6, out var s) && s == "Надпись\0🪵" &&
                data.ConnectionType == 2 && data.ConnectionHash == 17 && data.Persistent == true && data.Distant == false && data.Priority == 1,
                "Typed DataEntry sections or tail lost");
            Require(data.TryGet<byte[]>(7, out var first) && !data.TryGet<object>(7, out _) && !data.TryGet<int>(1, out _),
                "Unexpected type accepted");
            first[0] = 99;
            Require(data.TryGet<byte[]>(7, out var second) && second[0] == 1, "Reader exposes mutable stored bytes");
            for (int size = 0; size < all.Length; ++size)
            {
                var truncated = new byte[size]; Array.Copy(all, truncated, size);
                Reject(truncated);
            }
            var trailing = new byte[all.Length + 1]; Array.Copy(all, trailing, all.Length); Reject(trailing);
            Require(!BlueprintForeignZdo.TryRead(" " + Convert.ToBase64String(all), out _) &&
                !BlueprintForeignZdo.TryRead(new string('A', 32772), out _) && !BlueprintForeignZdo.TryRead("!", out _), "Unbounded/noncanonical Base64");
            Reject(Package(w => w.Write(32)));
            Reject(Package(w => { w.Write(1); w.Write((byte)2); w.Write(1); w.Write(1f); w.Write(1); w.Write(2f); }));
            var sharedHash = Package(w => { w.Write(17); w.Write((byte)1); w.Write(1); w.Write(1f);
                w.Write((byte)1); w.Write(1); w.Write("ChickenMeat"); });
            Require(BlueprintForeignZdo.TryRead(Convert.ToBase64String(sharedHash), out var typed) && typed.Count == 2 &&
                typed.TryGet<float>(1, out var time) && time == 1 && typed.TryGet<string>(1, out var food) && food == "ChickenMeat",
                "Valid cross-type key collision lost");
            Reject(Package(w => { w.Write(1); w.Write((byte)1); w.Write(1); w.Write(float.NaN); }));
            Reject(Package(w => { w.Write(128); w.Write((byte)1); w.Write(1); w.Write(int.MaxValue); }));
            Reject(Package(w => { w.Write(16); w.Write((byte)1); w.Write(1); w.Write((byte)1); w.Write((byte)255); }));
            Reject(Package(w => { w.Write(16); w.Write((byte)1); w.Write(1); w.Write(new byte[] { 128, 0 }); }));
            Reject(Package(w => { w.Write(512); w.Write((byte)2); }));
            LegacyItem();
        }
        private static void LegacyItem()
        {
            int Hash(string text) => text == "AxeIron" ? 100 : text.GetHashCode();
            byte[] packet = Package(w => {
                w.Write(89);
                w.Write((byte)1); w.Write(Hash("durability")); w.Write(12.349f);
                w.Write((byte)7);
                foreach (var pair in new[] { ("quality", 3), ("variant", 2), ("stack", 1), ("type", 1), ("dataCount", 1), ("pickedUp", 1), ("cheated", 1) })
                { w.Write(Hash(pair.Item1)); w.Write(pair.Item2); }
                w.Write((byte)1); w.Write(Hash("crafterID")); w.Write(42L);
                w.Write((byte)3);
                foreach (var pair in new[] { ("item", "AxeIron"), ("crafterName", "Ostrix"), ("data_0", "EpicLoot") })
                { w.Write(Hash(pair.Item1)); w.Write(pair.Item2); }
            });
            Require(BlueprintForeignZdo.TryRead(Convert.ToBase64String(packet), out var state), "Legacy test packet");
            Require(!BlueprintItemStandContents.TryConvertLegacy(state, Hash, null, out _, out _), "Missing host world level guessed");
            Require(BlueprintItemStandContents.TryConvertLegacy(state, Hash, 7, out var native, out var consumed) &&
                BlueprintItemStandContents.TryRead(native, Hash, out var item) && native.Info == "AxeIron:2:3:1" &&
                item.DurabilityHundredths == (int)(12.349f * 100f) && item.Stack == 1 && item.Quality == 3 && item.Variant == 2 &&
                item.WorldLevel == 7 && item.CrafterId == 42 && item.CrafterName == "Ostrix" && item.PickedUp && !item.Equipped &&
                item.Cheated && item.CustomData["EpicLoot"] == "" && consumed.Count == state.Count,
                "Legacy properties/defaults/custom empty fallback changed");
            Require(!BlueprintItemStandContents.TryConvertLegacy(state, Hash, 256, out _, out _), "Invalid host world level accepted");
            byte[] original = Package(w => { w.Write((byte)109); w.Write(10000); w.Write((byte)2); w.Write((byte)3);
                w.Write((byte)7); w.Write((byte)192); w.Write(100); w.Write((byte)2);
                w.Write("Z"); w.Write("first"); w.Write("A"); w.Write("second"); w.Write((byte)0); });
            byte[] outer = Package(w => { w.Write(144); w.Write((byte)1); w.Write(Hash("item")); w.Write("AxeIron");
                w.Write((byte)1); w.Write(Hash("itemData")); w.Write(original.Length); w.Write(original); });
            Require(BlueprintForeignZdo.TryRead(Convert.ToBase64String(outer), out var saved) &&
                BlueprintItemStandContents.TryConvertLegacy(saved, Hash, null, out var unchanged, out _) &&
                unchanged.Zdo == Convert.ToBase64String(original), "Native base item grid/world/custom insertion order changed");
            byte[] hashed = Package(w => { w.Write(136); w.Write((byte)1); w.Write(Hash("item")); w.Write(100);
                w.Write((byte)1); w.Write(Hash("itemData")); w.Write(original.Length); w.Write(original); });
            Require(BlueprintForeignZdo.TryRead(Convert.ToBase64String(hashed), out var hashedState) &&
                !BlueprintItemStandContents.TryConvertLegacy(hashedState, Hash, null, out _, out _) &&
                BlueprintItemStandContents.TryConvertLegacy(hashedState, Hash, null, out var hashedNative, out var hashedUsed, "AxeIron:0:1:0") &&
                hashedNative.Zdo == Convert.ToBase64String(original) && hashedUsed.Count == hashedState.Count,
                "Bound hashed item changed native packet or guessed unresolved name");
            foreach (string info in new[] { null, "", "Unknown:0:1:0", "AxeIron:0", "AxeIron:0:2:0", "AxeIron:1:1:0",
                "AxeIron:0:1:1", "AxeIron:0:0:0", "AxeIron:0:1:0:extra", "Axe/Iron:0:1:0" })
                Require(!BlueprintItemStandContents.TryConvertLegacy(hashedState, Hash, null, out _, out _, info),
                    "Invalid or conflicting hashed item manifest accepted");
        }
        private static byte[] Package(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            { write(writer); writer.Flush(); return stream.ToArray(); }
        }
        private static void Reject(byte[] bytes) => Require(!BlueprintForeignZdo.TryRead(Convert.ToBase64String(bytes), out var rejected) && rejected == null,
            "Malformed DataEntry accepted");
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}

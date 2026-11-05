using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class UnboundItemStandChecks
    {
        private const int Holder = -1235119491, Sword = -110263489;
        private static int Hash(string name) => name switch {
            "item" => -1126421579, "variant" => 1759467715, "quality" => 917906861,
            "type" => 1421151742, "itemData" => 949524933, _ => 123
        };
        private static void Check(bool condition, string why) { if (!condition) throw new InvalidOperationException(why); }
        private static UnboundBlueprint.Piece Piece(params (string Key, object Value)[] fields)
        {
            string packet = BlueprintForeignZdo.CreateNative(Holder, true, false, 2).WriteNative(
                fields.Select(field => new KeyValuePair<int, object>(Hash(field.Key), field.Value)));
            return UnboundBlueprint.Parse("vtbp 2\nitemstand|0|0|0|0|0|0|1|" + packet).Pieces.Single();
        }
        internal static void Run()
        {
            string Resolve(int id) => id == Sword ? "SwordIron" : null;
            var metadata = new BlueprintPrefabMetadata(1, "itemstand", "itemstand", "Piece", "substituted-holder-host", new Point3(1, 1, 1));
            var facts = new UnboundItemStandItem("SwordIron", 3, 4, new[] { 0, 1 });
            UnboundImportContext Context(UnboundItemStandItem item = null) => new UnboundImportContext(metadata, true, false, false, false,
                nativeHeader: (Holder, true, false, (byte)2), itemStandItems: new[] { item ?? facts });
            var context = Context(); var basis = new Point3(1, 1, 1);
            var packet = ItemStandContentsTests.Packet(Sword);
            var source = Piece(("item", Sword), ("variant", 2), ("quality", 3), ("type", 1), ("itemData", packet));
            Check(UnboundStateProjection.TryProject(source, context, Resolve, out var projected) &&
                projected.Contents.Info == "SwordIron:2:3:1" && projected.Contents.Zdo == Convert.ToBase64String(packet), "Holder projection lost exact data");
            var ordinary = new UnboundImportContext(metadata, true, true, false, false, nativeHeader: (Holder, true, false, (byte)2));
            Check(!UnboundStateProjection.TryProject(source, ordinary, Resolve, out _) &&
                !UnboundStateProjection.TryProject(source, Context(), _ => null, out _), "Ordinary or unresolved holder was admitted");
            Check(UnboundStateProjection.Matches(source, metadata, basis, false, null, null, projected.Contents) &&
                !UnboundStateProjection.Matches(source, metadata, basis, false, null, null, null), "Source proof dropped an attached item");
            var rotated = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, "SwordIron:2:3:0", projected.Contents.Zdo, null);
            Check(UnboundStateProjection.Matches(source, metadata, basis, false, null, null, rotated), "Supported orientation edit broke source binding");
            Check(UnboundStateProjection.TryEncode(source, context, basis, null, null, rotated, out string encoded), "Holder encoding failed");
            var output = UnboundBlueprint.Parse("vtbp 2\nitemstand|0|0|0|0|0|0|1|" + encoded).Pieces.Single();
            Check(UnboundStateProjection.TryProject(output, context, Resolve, out var roundtrip) && roundtrip.Contents.Info == rotated.Info &&
                roundtrip.Contents.Zdo == rotated.Zdo, "Holder reimport changed package/orientation");
            var limited = Context(new UnboundItemStandItem("SwordIron", 2, 2, Array.Empty<int>()));
            Check(UnboundStateProjection.TryProject(source, limited, Resolve, out _) &&
                !UnboundStateProjection.TryEncode(source, limited, basis, null, null, projected.Contents, out _),
                "Data inspection granted unsupported variant/quality/orientation restoration");
            var cleared = projected.Contents.WithoutContents(name => name == "SwordIron" ? Sword : 0);
            Check(UnboundStateProjection.Matches(source, metadata, basis, false, null, null, cleared), "Clear did not preserve source proof");
            Check(UnboundStateProjection.TryEncode(source, limited, basis, null, null, cleared, out string clearedPacket), "Clear encoding failed");
            var empty = UnboundBlueprint.Parse("vtbp 2\nitemstand|0|0|0|0|0|0|1|" + clearedPacket).Pieces.Single();
            Check(empty.State.TryGet<int>(Hash("item"), out int id) && id == 0 && !empty.State.HasHash(Hash("itemData")) &&
                UnboundStateProjection.TryProject(empty, context, null, out var emptyState) && emptyState.Contents == null,
                "Cleared holder resurrected archived itemData");
            var stale = Piece(("item", 0), ("itemData", new byte[] { 255 }), ("quality", -3));
            Check(UnboundStateProjection.TryProject(stale, context, null, out var staleState) && staleState.Contents == null &&
                UnboundStateProjection.Matches(stale, metadata, basis, false, null, null, null), "Inactive native fields were treated as an item");
            foreach (var bad in new[] {
                Piece(("item", Sword), ("variant", 2), ("quality", 3)),
                Piece(("item", Sword), ("variant", 2), ("quality", 3), ("itemData", packet), ("unknown", 7)),
                Piece(("item", Sword), ("variant", 2), ("quality", 3), ("itemData", packet), ("quality", "3")),
                Piece(("item", Sword), ("variant", 2), ("quality", 3), ("itemData", new byte[3])) })
                Check(!UnboundStateProjection.TryProject(bad, context, Resolve, out var partial) && partial == null, "Malformed/residual holder state partially applied");
            var wrongFormat = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, "1:1:0:0", "bgAAAAAA", null, true);
            Check(!UnboundStateProjection.TryEncode(null, context, basis, null, null, wrongFormat, out _), "Cleared container passed holder encoder");
            int[] orientations = { 1, 0 }; var immutable = new UnboundItemStandItem("SwordIron", 3, 4, orientations); orientations[0] = 9;
            Check(immutable.Orientations.SequenceEqual(new[] { 0, 1 }) && immutable.Equals(facts), "Caller mutated catalog facts");
            bool mixed = false;
            try { _ = new UnboundImportContext(metadata, true, true, false, false, itemStandItems: new[] { facts }); }
            catch (ArgumentException) { mixed = true; }
            Check(mixed, "Mixed ordinary/holder permission accepted");
            Check(source.ZdoData != encoded && source.State.TryGet<byte[]>(Hash("itemData"), out var original) && original.SequenceEqual(packet),
                "Holder encoding mutated immutable producer state");
            Console.WriteLine("PASS Unbound holder projection: exact native item data, scoped facts, edited orientation, clear/empty, unsupported caps and malformed/ordinary/stale-state controls; synthetic CPU only");
        }
    }
}

using System;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class ArmorStandStateChecks
    {
        internal static void Run()
        {
            void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
            void Reject(Action action)
            { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Invalid armor envelope accepted."); }
            int Hash(string name) => name == "SwordIron" ? -101 : 92;
            byte[] packet = ItemStandContentsTests.Packet(); packet[0] = 110;
            var live = new BlueprintArmorStandState.Slot(5, -101, "SwordIron", 2, packet);
            var stale = new BlueprintArmorStandState.Slot(5, 0, null, 7, packet);
            var unknown = new BlueprintArmorStandState.Slot(-7, 92, null, -3, new byte[] { 111, 9 });
            var state = new BlueprintArmorStandState(int.MinValue, new[] { live, stale, unknown,
                new BlueprintArmorStandState.Slot(9, 0, null, 0, null),
                new BlueprintArmorStandState.Slot(9, 0, null, 0, Array.Empty<byte>()) });
            var data = state.ToForeignData(); var decoded = BlueprintArmorStandState.Read(data);
            Require(decoded.Pose == int.MinValue && decoded.Slots.Count == 5 && decoded.Slots[0].VisualSlot == 5 &&
                decoded.Slots[1].ItemId == 0 && decoded.Slots[2].Prefab == null && decoded.Slots[2].Variant == -3 &&
                decoded.Slots[3].Package == null && decoded.Slots[4].Package.Length == 0, "Pose/order/unknown/null evidence changed.");
            Require(live.TryInspect(Hash, out var item) && item.Quality == 3 && item.Variant == 2 &&
                item.SourceVersion == 110 && item.Cheated && item.CustomData.Count == 1, "Native equipment not inspected exactly.");
            Require(!stale.TryInspect(Hash, out _) && !unknown.TryInspect(Hash, out _), "Inactive or unknown equipment activated.");
            Require(!new BlueprintArmorStandState.Slot(0, 99, "SwordIron", 2, packet).TryInspect(Hash, out _) &&
                !new BlueprintArmorStandState.Slot(0, -101, "SwordIron", 1, packet).TryInspect(Hash, out _), "Mismatched item accepted.");
            var cleared = data.WithoutContents(Hash);
            Require(cleared.ContentsCleared && cleared.Info == data.Info && cleared.Zdo == data.Zdo && !data.ContentsCleared,
                "Disabling equipment erased pose/archive or mutated original.");
            var part = new BlueprintEditorPart("armor", "ArmorStand", "Armor", new Point3(), new Rotation3(0, 0, 0, 1), foreignData: data);
            var editor = new BlueprintEditorDocument("armor-doc", "Armor", "QA", new[] { part }, Array.Empty<BlueprintEditorGroup>());
            Require(editor.SetPartProperties("armor", "Moved", new Point3(2, 3, 4), new Rotation3(0, 0, 0, 1)) &&
                editor.Parts[0].ForeignData.Zdo == data.Zdo && editor.Undo() && editor.Redo() &&
                editor.Parts[0].ForeignData.Info == data.Info, "Editor transform/history lost armor evidence.");
            packet[0] = 0; byte[] owned = live.Package; owned[0] = 0;
            Require(live.Package[0] == 110 && decoded.ToForeignData().Zdo == data.Zdo, "Mutable package escaped.");
            var bytes = Convert.FromBase64String(data.Zdo);
            for (int n = 0; n < bytes.Length; ++n)
            {
                string truncated = Convert.ToBase64String(bytes.Take(n).ToArray());
                Reject(() => new BlueprintForeignPartData(BlueprintForeignPartData.NativeArmorStandFormat, data.Info, truncated, null));
            }
            foreach (string info in new[] { "0:65", "0:-1", "0:2147483648", "01:0", "0:00", "+0:0", "0:0:0", "0:" })
                Reject(() => new BlueprintForeignPartData(BlueprintForeignPartData.NativeArmorStandFormat, info, "AQ==", null));
            foreach (string encoded in new[] { "", " AQ==", "Ag==", "AQAA", "!", "AQAAAAAA////f////3////9/" })
                Reject(() => new BlueprintForeignPartData(BlueprintForeignPartData.NativeArmorStandFormat, "0:1", encoded, null));
            Reject(() => new BlueprintForeignPartData(BlueprintForeignPartData.NativeArmorStandFormat, "0:0", "AQ==", "1"));
            Reject(() => new BlueprintArmorStandState(0, Enumerable.Repeat(live, 65)));
            Reject(() => new BlueprintArmorStandState.Slot(0, 1, "bad/name", 0, null));
            Reject(() => new BlueprintArmorStandState.Slot(0, 1, null, 0, new byte[24577]));
            Reject(() => new BlueprintArmorStandState(0, Enumerable.Repeat(
                new BlueprintArmorStandState.Slot(0, 0, null, 0, new byte[24576]), 64)).ToForeignData());
            Require(BlueprintArmorStandState.Read(new BlueprintArmorStandState(3, Array.Empty<BlueprintArmorStandState.Slot>()).ToForeignData()).Pose == 3,
                "Empty stand lost its pose.");
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using OstrixMods.BuildWorks.Geometry;

internal static class Program
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string[] folders = { Path.Combine(root, "artifacts", "bin"),
            Path.GetFullPath(Path.Combine(root, "..", "BepInEx", "core")),
            @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed" };
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, new AssemblyName(e.Name).Name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        Assembly asm = Assembly.LoadFrom(Path.Combine(root, "artifacts", "bin", "BuildWorks.dll"));
        var document = new BlueprintEditorDocument(null, "Scale regression", "OTHER",
            new[] { new BlueprintEditorPart("a", "woodwall", "A", new Point3(),
                new Rotation3(0,0,0,1), scale: new Point3(2,0.5,1.25), signText: "Дом\nOstrix"), Part("b",2) });
        Type controllerType = asm.GetType("OstrixMods.BuildWorks.BlueprintEditorController", true);
        object controller = FormatterServices.GetUninitializedObject(controllerType);
        controllerType.GetField("document", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, document);
        object parts = controllerType.GetMethod("BuildStoreParts", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
        Type storeType = asm.GetType("OstrixMods.BuildWorks.CompositeBlueprintStore", true);
        string pathOut = Path.Combine(args[1], "bridge-roundtrip-" + Guid.NewGuid().ToString("N") + ".json");
        object store = Activator.CreateInstance(storeType, BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { pathOut }, null);
        MethodInfo save = storeType.GetMethods().Single(m => m.Name == "TrySaveDocument" && m.GetParameters().Length == 7);
        object[] call = { "Scale regression", "OTHER", parts,
            Array.CreateInstance(storeType.GetNestedType("Group", BindingFlags.NonPublic), 0),
            Array.CreateInstance(storeType.GetNestedType("VectorData", BindingFlags.NonPublic), 0), null, null };
        Require((bool)save.Invoke(store, call), "Store rejected Controller bridge: " + call[6]);
        object savedPart = ((IList)call[5].GetType().GetField("parts").GetValue(call[5]))[0];
        object scale = savedPart.GetType().GetField("scale").GetValue(savedPart);
        Require((float)scale.GetType().GetField("x").GetValue(scale) == 2f &&
            (float)scale.GetType().GetField("y").GetValue(scale) == 0.5f &&
            (float)scale.GetType().GetField("z").GetValue(scale) == 1.25f,
            "Nonuniform scale lost between Controller and Store");
        Require(File.ReadAllText(pathOut).Contains("\"scale\""), "Disk scale missing");
        Require((string)savedPart.GetType().GetField("signText").GetValue(savedPart) == "Дом\nOstrix", "Controller stripped sign text");
        var restored = (BlueprintEditorDocument)controllerType.GetMethod("DocumentFromBlueprint", BindingFlags.NonPublic|BindingFlags.Static)
            .Invoke(null, new[] {call[5]});
        Require(restored.Parts[0].SignText == "Дом\nOstrix", "Controller reopen stripped sign text");
        Console.WriteLine("PASS actual Controller.BuildStoreParts -> Store.TrySaveDocument preserves XYZ scale");
        Console.WriteLine("PASS actual Controller/store/reopen preserves sign text");

        document.AddSupport(new BlueprintEditorSupport("support", "Вход", new Point3(2,3,4), new Rotation3(0,1,0,1)));
        document.SetPlacementSupport("support");
        document.SelectOnly("a");
        document.SelectSupport("support");
        var evidence = new BlueprintSourceDocument("blueprint", "Original.blueprint", "\uFEFF#Pieces\r\nwoodwall;;0;0;0;0;0;0;1;\"\"\r\nwoodwall;;2;0;0;0;0;0;1;\"\"\r\n");
        var payloadParts = document.Parts.Select((part, index) => new BlueprintEditorPart(part.StableId, part.PrefabName,
            part.DisplayName, part.Position, part.Rotation, part.ParentGroupId, part.Visible, part.Locked, part.Scale,
            part.SignText, evidence.Id, index + 2, index == 0
                ? new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, "SwordIron:2:3:0",
                    Convert.ToBase64String(new byte[] { 109, 3, 4, 5 }), null, true)
                : new BlueprintForeignPartData("blueprint", "\"SwordIron:0:1:3\"", "opaque", ".5")));
        var payload = new BlueprintEditorDocument(null, "Copy", "OTHER", payloadParts,
            sourceCustomSnapPoints: new[] { new Point3(7,8,9) }, sourceSupports: document.Supports,
            placementSupportId: document.PlacementSupportId, sourceDocuments: new[] { evidence });
        payload.SelectOnly("a"); payload.SelectSupport("support");
        var copied = (BlueprintEditorDocument)controllerType.GetMethod("CopyDocument", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { payload });
        Require(copied.CustomSnapPoints.Count == 1 && copied.CustomSnapPoints[0].Y == 8 &&
            copied.Supports.Count == 1 && copied.Supports[0].Position.Z == 4 &&
            copied.PlacementSupportId == "support" && copied.ActiveSupportId == "support" &&
            copied.Selection.SequenceEqual(payload.Selection) && !ReferenceEquals(copied.Supports[0], payload.Supports[0]),
            "Controller copy dropped root payload or conflated support/object selection");
        copied.SetSupportPose("support", new Point3(9,9,9), new Rotation3(0,0,0,1));
        Require(payload.Supports[0].Position.X == 2, "Controller copy aliases support poses");
        Require(copied.Sources[0].Text == evidence.Text && copied.Parts[0].SourceDocumentId == evidence.Id,
            "Controller copy discarded source provenance");
        Require(copied.Parts[0].ForeignData.Info == payload.Parts[0].ForeignData.Info &&
            copied.Parts[0].ForeignData.ContentsCleared, "Controller copy dropped foreign object data or clear intent");
        Console.WriteLine("PASS actual Controller.CopyDocument retains detached root payload and support scope");

        controllerType.GetField("document", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, payload);
        object supportData = controllerType.GetMethod("BuildStoreSupports", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
        parts = controllerType.GetMethod("BuildStoreParts", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
        object sourceData = controllerType.GetMethod("BuildStoreSources", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
        MethodInfo supportSave = storeType.GetMethods().Single(m => m.Name == "TrySaveDocument" && m.GetParameters().Length == 14);
        object[] supportCall = { "Support bridge", "OTHER", parts, call[3], call[4], null, null,
            supportData, payload.PlacementSupportId, null, null, null, null, sourceData };
        Require((bool)supportSave.Invoke(store, supportCall), "Store rejected support bridge: " + supportCall[10]);
        object reloadedStore = Activator.CreateInstance(storeType, BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { pathOut }, null);
        object reloadedBlueprint = ((IList)storeType.GetMethod("All").Invoke(reloadedStore, null))[1];
        var reloaded = (BlueprintEditorDocument)controllerType.GetMethod("DocumentFromBlueprint", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new[] { reloadedBlueprint });
        Require(reloaded.Supports.Count == 1 && reloaded.Supports[0].StableId == "support" &&
            reloaded.Supports[0].Name == "Вход" && reloaded.Supports[0].Position.Z == 4 &&
            Math.Abs(reloaded.Supports[0].Rotation.Y - Math.Sqrt(.5)) < .000001 &&
            reloaded.PlacementSupportId == "support" && reloaded.ActiveSupportId == null && reloaded.Selection.Count == 0 &&
            reloaded.Parts[0].Position.X == 0 && reloaded.Parts[0].SignText == "Дом\nOstrix",
            "Controller support disk roundtrip lost pose/default, moved geometry or persisted transient focus");
        Console.WriteLine("PASS actual Controller.BuildStoreSupports -> disk -> DocumentFromBlueprint preserves root pose/default without transient focus");
        Require(reloaded.Sources[0].Text == evidence.Text && reloaded.Sources[0].Id == evidence.Id &&
            reloaded.Parts[0].SourceDocumentId == evidence.Id && reloaded.Parts[0].SourceLine == 2,
            "Actual Controller source/part bridge discarded original bytes or row binding");
        Require(reloaded.Parts[0].ForeignData.ContentsCleared &&
            reloaded.Parts[0].ForeignData.Info == payload.Parts[0].ForeignData.Info &&
            reloaded.Parts[1].ForeignData.Zdo == "opaque" &&
            reloaded.Parts[0].ForeignData.Format == BlueprintForeignPartData.NativeItemStandFormat &&
            reloaded.Parts[0].ForeignData.Zdo == payload.Parts[0].ForeignData.Zdo,
            "Actual controller/store/reopen dropped part-bound foreign state");
        Console.WriteLine("PASS actual Controller copy/build/save/reopen retains part-bound opaque holder/object state");
            Console.WriteLine("PASS actual Controller sources/row references -> store -> disk -> reopen retains content evidence");

        var grouped = new BlueprintEditorDocument(null, "Group", "OTHER", new[] { Part("a",0), Part("b",2) });
        grouped.SelectOnly("a"); grouped.ToggleSelection("b"); grouped.CreateGroup("g", "Group");
        grouped.DuplicateSelection(new Point3(10,0,0));
        Require(grouped.Groups.Count == 2 && grouped.Parts.Count == 4, "Group tree was not copied");
        grouped.SelectOnly("g");
        grouped.ApplyTransformDelta(new Point3(1,0,0), new Rotation3(0,0,0,1), new Point3());
        Require(grouped.Parts.Where(p => p.ParentGroupId != "g").Select(p => p.Position.X).SequenceEqual(new[] {10d,12d}),
            "Original group transform moved copied group");
        Console.WriteLine("PASS independent group duplicate");

        var nested = new BlueprintEditorDocument(null,"Nested","OTHER",
            new[] { new BlueprintEditorPart("p","woodwall","Part",new Point3(),new Rotation3(0,0,0,1),"inner") },
            new[] { new BlueprintEditorGroup("outer","Outer"), new BlueprintEditorGroup("inner","Inner",parentGroupId:"outer") });
        nested.SelectOnly("outer");
        Type sceneType = asm.GetType("OstrixMods.BuildWorks.BlueprintEditorScene",true);
        var expanded = (IList)sceneType.GetMethod("ExpandedPartIds",BindingFlags.NonPublic|BindingFlags.Static)
            .Invoke(null, new object[] { nested, nested.Selection });
        Require(nested.IsPartSelected("p") && expanded.Count == 1 && (string)expanded[0] == "p",
            "Scene selection diverged from document at nested leaf");
        Console.WriteLine("PASS actual Scene nested selection expansion agrees with document");
        return 0;
    }
    private static BlueprintEditorPart Part(string id,double x) =>
        new BlueprintEditorPart(id,"woodwall",id,new Point3(x,0,0),new Rotation3(0,0,0,1));
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}

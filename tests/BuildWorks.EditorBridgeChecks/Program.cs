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
                new Rotation3(0,0,0,1), scale: new Point3(2,0.5,1.25)), Part("b",2) });
        Type controllerType = asm.GetType("OstrixMods.BuildWorks.BlueprintEditorController", true);
        object controller = FormatterServices.GetUninitializedObject(controllerType);
        controllerType.GetField("document", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, document);
        object parts = controllerType.GetMethod("BuildStoreParts", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
        Type storeType = asm.GetType("OstrixMods.BuildWorks.CompositeBlueprintStore", true);
        string pathOut = Path.Combine(args[1], "bridge-roundtrip.json");
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
        Console.WriteLine("PASS actual Controller.BuildStoreParts -> Store.TrySaveDocument preserves XYZ scale");

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

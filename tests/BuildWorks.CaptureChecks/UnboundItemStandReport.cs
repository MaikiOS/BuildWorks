using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

// Checks the actual compiled QA serializer without starting Unity.
internal static class UnboundItemStandReportChecks
{
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name).SetValue(target, value); }
    public static int Main(string[] args)
    {
        if (args.Length != 4) throw new ArgumentException("normal DLL, QA DLL, BepInEx core, Valheim Managed required");
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request) {
            string name = new AssemblyName(request.Name).Name;
            if (name != Path.GetFileName(name)) throw new InvalidOperationException("Invalid assembly name");
            foreach (string root in new[] { Path.GetDirectoryName(args[1]), args[2], args[3] }) {
                string path = Path.Combine(root, name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        const string nameSpace = "OstrixMods.BuildWorks.BuildWorksTestItemStandProfiles";
        Check(Assembly.LoadFile(Path.GetFullPath(args[0])).GetType(nameSpace) == null, "QA reader leaked into normal assembly");
        var assembly = Assembly.LoadFile(Path.GetFullPath(args[1]));
        var type = assembly.GetType(nameSpace, true);
        var names = (string[])type.GetField("ProbeNames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Check(String.Join("|", names) == "itemstand|itemstandh|wood_floor|piece_chest_wood|ArmorStand", "Named native controls changed");
        var reportType = type.GetNestedType("Report", BindingFlags.NonPublic);
        var profileType = type.GetNestedType("Profile", BindingFlags.NonPublic);
        var itemType = type.GetNestedType("Item", BindingFlags.NonPublic);
        var report = Activator.CreateInstance(reportType);
        var profile = Activator.CreateInstance(profileType);
        var item = Activator.CreateInstance(itemType);
        Set(item, "name", "SwordIron\u0442"); Set(item, "hash", -110263489);
        Set(item, "variants", 3); Set(item, "maximumQuality", 4); Set(item, "orientations", new[] { 0, 2 });
        var items = Array.CreateInstance(itemType, 1); items.SetValue(item, 0); Set(profile, "items", items);
        Set(profile, "name", "itemstand"); Set(profile, "holder", true); Set(profile, "header", true);
        Set(profile, "prefabHash", -1235119491); Set(profile, "objectType", 2); Set(profile, "persistent", true);
        Set(profile, "prefabScale", new[] { 1.0, 2.0, 3.0 });
        var profiles = Array.CreateInstance(profileType, 2); profiles.SetValue(profile, 0);
        var negative = Activator.CreateInstance(profileType); Set(negative, "name", "wood_floor"); profiles.SetValue(negative, 1);
        Set(report, "profiles", profiles); Set(report, "process", 123); Set(report, "passed", true);
        var serialize = type.GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic);
        string json = (string)serialize.Invoke(null, new[] { report });
        var decoded = new JavaScriptSerializer().DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>;
        Check(decoded != null && (bool)decoded["passed"] && (int)decoded["process"] == 123, "Report scalars missing");
        var parsedProfiles = (object[])decoded["profiles"];
        Check(parsedProfiles.Length == 2, "Profile array omitted");
        var first = (System.Collections.Generic.Dictionary<string, object>)parsedProfiles[0];
        var scale = (object[])first["prefabScale"];
        Check((int)first["prefabHash"] == -1235119491 && (int)first["objectType"] == 2 && (bool)first["persistent"] &&
            scale.Length == 3 && Convert.ToDouble(scale[1]) == 2.0, "Native header/scale facts missing");
        var parsedItem = (System.Collections.Generic.Dictionary<string, object>)((object[])first["items"])[0];
        Check((string)parsedItem["name"] == "SwordIron\u0442" && (int)parsedItem["hash"] == -110263489 &&
            (int)parsedItem["variants"] == 3 && (int)parsedItem["maximumQuality"] == 4, "Item facts changed");
        var orientations = (object[])parsedItem["orientations"];
        Check(orientations.Length == 2 && (int)orientations[0] == 0 && (int)orientations[1] == 2, "Orientation array omitted");
        Check(((System.Collections.Generic.Dictionary<string, object>)parsedProfiles[1])["items"] == null, "Negative null catalog changed");
        Set(item, "orientations", new int[0]); Set(profile, "items", Array.CreateInstance(itemType, 0));
        Check(((string)serialize.Invoke(null, new[] { report })).Contains("\"items\":[]"), "Empty catalog omitted");
        var snapshot = type.GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic);
        try { snapshot.Invoke(null, new object[] { null }); throw new Exception("Missing guard accepted"); }
        catch (TargetInvocationException error) { Check(error.InnerException is ArgumentNullException, "Wrong missing-guard failure: " + error.InnerException); }
        var sentinel = new InvalidOperationException("stop before host access");
        try { snapshot.Invoke(null, new object[] { (Action)(() => { throw sentinel; }) }); throw new Exception("Guard skipped"); }
        catch (TargetInvocationException error) { Check(Object.ReferenceEquals(error.InnerException, sentinel), "Host accessed before caller guard"); }
        Console.WriteLine("PASS actual QA DTO arrays/scalars/null/empty/Unicode/header/scale, five names, normal exclusion, missing/throwing pre-host guards; native profile calls NOTRUN.");
        return 0;
    }
}

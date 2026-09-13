using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using OstrixMods.BuildWorks.Geometry;

internal static class Program
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type sessionType, blueprintType, partType, vectorType, rotationType, pieceType;
    private static MethodInfo append, appendAtPivot;

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
        try
        {
            Assembly asm = Assembly.LoadFrom(Path.Combine(root, "artifacts", "bin", "BuildWorks.dll"));
            sessionType = asm.GetType("OstrixMods.BuildWorks.PrecisionPlacementSession", true);
            Type storeType = asm.GetType("OstrixMods.BuildWorks.CompositeBlueprintStore", true);
            blueprintType = storeType.GetNestedType("Blueprint", BindingFlags.NonPublic);
            partType = storeType.GetNestedType("Part", BindingFlags.NonPublic);
            append = sessionType.GetMethod("TryAddPlacementInstance", Instance);
            appendAtPivot = sessionType.GetMethod("TryAddBlueprintLayoutInstance", Instance);
            vectorType = append.GetParameters()[0].ParameterType;
            rotationType = append.GetParameters()[1].ParameterType;
            pieceType = sessionType.GetField("selectedPiece", Instance).FieldType;
            TestGridComposition();
            TestPivotLayoutComposition();
            TestHistorySurvivesPivotFrameSwitch();
            TestRotatedRoots();
            TestLimit();
            TestWorldCaptureScale();
            TestUniformGroupScale();
            TestUniformSingleScale();
            TestInvalidGroupScaleIsAtomic();
            TestPrimaryPartSelection();
            Console.WriteLine("All actual Session root-composition checks passed; no game placement executed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void TestPivotLayoutComposition()
    {
        Require(appendAtPivot != null,
            "Session has no explicit blueprint pivot-to-root layout boundary");
        object session = Session(2);
        IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
        Set(parts[0], "scale", StoredVector(1f, 1f, 1f));
        Set(parts[1], "scale", StoredVector(1f, 1f, 1f));
        Set(session, "blueprintPivotLocal", Activator.CreateInstance(vectorType, 0f, 2f, 0f));
        float s = (float)Math.Sqrt(0.5);
        object rotation = Rotation(0, 0, s, s);
        object pivot = Activator.CreateInstance(vectorType, 10f, 20f, 30f);
        Require((bool)appendAtPivot.Invoke(session, new[] { pivot, rotation, (object)1.5f }),
            "Blueprint layout append rejected a valid pivot pose");
        IList plan = (IList)Get(session, "placementPlan");
        CheckVector(Property(plan[0], "Position"), new Point3(13, 21.5, 30));
        CheckVector(Property(plan[1], "Position"), new Point3(10, 20, 30));
        Console.WriteLine("PASS actual Session: rotated non-root blueprint pivot remains fixed through first scaled layout expansion");
    }

    private static void TestHistorySurvivesPivotFrameSwitch()
    {
        object session = Session(2);
        Set(session, "blueprintEditPartIndex", -1);
        Set(session, "blueprintPivotLocal", Activator.CreateInstance(vectorType, 0f, 2f, 0f));
        Set(session, "currentPosition", Activator.CreateInstance(vectorType, 10f, 20f, 30f));
        Set(session, "currentRotation", Rotation(0, 0, 0, 1));
        MethodInfo capture = sessionType.GetMethod("CaptureHistorySnapshot", Instance);
        MethodInfo apply = sessionType.GetMethod("ApplyHistorySnapshot", Instance);
        object snapshot = capture.Invoke(session, null);
        CheckVector(Property(snapshot, "Position"), new Point3(10, 18, 30));

        Set(session, "blueprintPivotLocal", Activator.CreateInstance(vectorType, 0f, 0f, 0f));
        Set(session, "currentPosition", Activator.CreateInstance(vectorType, 12f, 18f, 30f));
        apply.Invoke(session, new[] { snapshot });
        CheckVector(Get(session, "blueprintRootPosition"), new Point3(10, 18, 30));
        CheckVector(Get(session, "currentPosition"), new Point3(10, 18, 30));
        Console.WriteLine("PASS actual Session: whole-blueprint history restores root across a composite-frame key switch");
    }

    private static void TestGridComposition()
    {
        object session = Session(2);
        var roots = GuidePathSampling.SamplePlane(new Point3(10,20,30),
            new Point3(3,0,0), 3, new Point3(0,0,4), 2, false);
        float s = (float)Math.Sqrt(0.5);
        foreach (Point3 point in roots)
            Require(Add(session, point, Rotation(0,0,s,s)), "3x2 group append failed");
        IList plan = (IList)Get(session, "placementPlan");
        IList sources = (IList)Get(session, "placementPlanPieces");
        IList original = (IList)Get(session, "activeBlueprintPieces");
        Require(plan.Count == 12 && sources.Count == 12, "3x2 layout did not expand every source part");
        for (int root = 0; root < roots.Count; ++root)
        {
            CheckVector(Property(plan[root * 2], "Position"), roots[root] + new Point3(0,1,0));
            CheckVector(Property(plan[root * 2 + 1], "Position"), roots[root] + new Point3(-2,0,0));
            CheckVector(Property(plan[root * 2], "Scale"), new Point3(2,0.5,1.25));
            CheckVector(Property(plan[root * 2 + 1], "Scale"), new Point3(0.01,4,1));
            CheckRotation(Property(plan[root * 2 + 1], "Rotation"), 0.5f,0.5f,0.5f,0.5f);
            Require(ReferenceEquals(sources[root * 2], original[0]) &&
                ReferenceEquals(sources[root * 2 + 1], original[1]), "Native source order changed");
        }
        IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
        CheckVector(Get(parts[0], "position"), new Point3(1,0,0));
        CheckVector(Get(parts[1], "scale"), new Point3(0.01,4,1));
        Require((int)Get(session, "placementPlanIndex") == 0, "Preview advanced native placement index");
        Console.WriteLine("PASS actual Session: 3x2 root grid expands ordered parts, position/rotation/XYZ scale; source/index unchanged");
    }

    private static void TestRotatedRoots()
    {
        object session = Session(2);
        Require(Add(session, new Point3(10,0,0), Rotation(0,0,0,1)), "First contour root failed");
        Require(Add(session, new Point3(20,5,0), Rotation(0,0,1,0)), "Second contour root failed");
        IList plan = (IList)Get(session, "placementPlan");
        CheckVector(Property(plan[0], "Position"), new Point3(11,0,0));
        CheckVector(Property(plan[1], "Position"), new Point3(10,2,0));
        CheckVector(Property(plan[2], "Position"), new Point3(19,5,0));
        CheckVector(Property(plan[3], "Position"), new Point3(20,3,0));
        Console.WriteLine("PASS actual Session: differently rotated contour root poses preserve complete group offsets");
    }

    private static void TestLimit()
    {
        object session = Session(128);
        for (int index = 0; index < 4; ++index)
            Require(Add(session, new Point3(index,0,0), Rotation(0,0,0,1)), "Valid 512-part plan failed");
        IList plan = (IList)Get(session, "placementPlan");
        object last = plan[511];
        Require(!Add(session, new Point3(4,0,0), Rotation(0,0,0,1)), "Oversized plan accepted");
        Require(plan.Count == 512 && ((IList)Get(session, "placementPlanPieces")).Count == 512 &&
            last.Equals(plan[511]), "Rejected group was partially appended or changed earlier entries");
        Require((bool)Get(session, "expandedPlanLimited"), "Oversized plan did not block arming");
        Require((int)Get(session, "placementPlanIndex") == 0, "Limit check advanced native placement index");
        Console.WriteLine("PASS actual Session: 512 actual parts allowed; next whole group rejected atomically and arming guard set");
    }

    private static object Session(int partCount)
    {
        object session = RuntimeHelpers.GetUninitializedObject(sessionType);
        object blueprint = Activator.CreateInstance(blueprintType);
        IList parts = (IList)Get(blueprint, "parts");
        float s = (float)Math.Sqrt(0.5);
        for (int index = 0; index < partCount; ++index)
        {
            object part = Activator.CreateInstance(partType);
            Set(part, "stableId", "part-" + index);
            Set(part, "position", StoredVector(index == 1 ? 0f : 1f, index == 1 ? 2f : 0f, 0f));
            Set(part, "scale", index == 1 ? StoredVector(0.01f,4f,1f) : StoredVector(2f,0.5f,1.25f));
            object rotation = Activator.CreateInstance(partType.GetField("rotation").FieldType);
            Set(rotation, "x", index == 1 ? s : 0f);
            Set(rotation, "w", index == 1 ? s : 1f);
            Set(part, "rotation", rotation);
            parts.Add(part);
        }
        Set(session, "activeBlueprint", blueprint);
        foreach (string field in new[] { "placementPlan", "placementPlanPieces", "activeBlueprintPieces" })
            Set(session, field, Activator.CreateInstance(sessionType.GetField(field, Instance).FieldType));
        IList sources = (IList)Get(session, "activeBlueprintPieces");
        for (int index = 0; index < partCount; ++index)
            sources.Add(RuntimeHelpers.GetUninitializedObject(pieceType));
        return session;
    }

    private static void TestPrimaryPartSelection()
    {
        object session = Session(2);
        object blueprint = Get(session, "activeBlueprint");
        Type store = blueprintType.DeclaringType;
        MethodInfo primaryPart = store.GetMethod(
            "PrimaryPartIndex", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo framePart = store.GetMethod(
            "FramePartIndex", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo isFramePart = store.GetMethod(
            "IsFramePart", BindingFlags.Static | BindingFlags.NonPublic);
        Require((int)primaryPart.Invoke(null, new[] { blueprint }) == -1,
            "Blueprint without an explicit primary part did not keep composite framing");

        IList groups = (IList)Get(blueprint, "groups");
        Type groupType = groups.GetType().GetGenericArguments()[0];
        object group = Activator.CreateInstance(groupType);
        Set(group, "stableId", "frame-group");
        Set(group, "name", "Frame");
        groups.Add(group);
        IList parts = (IList)Get(blueprint, "parts");
        Set(parts[0], "parentGroupId", "frame-group");
        Set(blueprint, "primaryGroupId", "frame-group");
        Require((int)framePart.Invoke(null, new[] { blueprint }) == 0,
            "Primary group did not select its first member as host frame");
        Require((bool)isFramePart.Invoke(null, new[] { blueprint, (object)0 }),
            "Primary group member was not part of the world frame");
        Require(!(bool)isFramePart.Invoke(null, new[] { blueprint, (object)1 }),
            "Part outside primary group leaked into the world frame");

        Set(blueprint, "primaryPartId", "part-1");
        Require((int)framePart.Invoke(null, new[] { blueprint }) == 1,
            "Explicit primary part did not select its own frame");
        Require(!(bool)isFramePart.Invoke(null, new[] { blueprint, (object)0 }) &&
            (bool)isFramePart.Invoke(null, new[] { blueprint, (object)1 }),
            "Primary part did not override the primary group frame");
        Set(blueprint, "primaryPartId", "missing");
        Require((int)framePart.Invoke(null, new[] { blueprint }) == 0,
            "Missing primary part did not fall back to the primary group");
        Console.WriteLine("PASS actual Store/Session: primary part overrides primary group; group frame excludes outside parts and supplies the host piece");
    }

    private static void TestWorldCaptureScale()
    {
        MethodInfo method = sessionType.GetMethod("RelativeBlueprintScale", BindingFlags.Static | BindingFlags.NonPublic);
        float source = 0.84671533f;
        object basis = Activator.CreateInstance(vectorType, source, 2f, 0.5f);
        object world = Activator.CreateInstance(vectorType, source * 0.01f, 8f, 0.75f);
        object value = method.Invoke(null, new[] { world, basis });
        Require((float)Get(value, "x") == 0.01f, "World 1% scale was not normalized at float boundary");
        CheckVector(value, new Point3(0.01, 4, 1.5));
        foreach (float invalid in new[] { 0f, 0.009f, 4.001f, float.NaN, float.PositiveInfinity })
        {
            bool rejected = false;
            try { method.Invoke(null, new[] { Activator.CreateInstance(vectorType, invalid, 1f, 1f),
                Activator.CreateInstance(vectorType, 1f, 1f, 1f) }); }
            catch (TargetInvocationException error) when (error.InnerException is ArgumentOutOfRangeException)
            { rejected = true; }
            Require(rejected, "Invalid world scale accepted: " + invalid);
        }
        Console.WriteLine("PASS actual Session: world capture preserves relative prefab scale and bounded 1% float roundoff; invalid scales rejected");
    }

    private static void TestUniformGroupScale()
    {
        object session = Session(2);
        IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
        Set(parts[0], "scale", StoredVector(0.5f, 0.25f, 1f));
        Set(parts[1], "scale", StoredVector(1f, 2f, 0.5f));
        float s = (float)Math.Sqrt(0.5);
        Require(Add(session, new Point3(10,20,30), Rotation(0,0,s,s), 2f), "200% whole group rejected");
        Require(Add(session, new Point3(40,50,60), Rotation(0,0,s,s), 0.5f), "50% whole group rejected");
        IList plan = (IList)Get(session, "placementPlan");
        CheckVector(Property(plan[0], "Position"), new Point3(10,22,30));
        CheckVector(Property(plan[1], "Position"), new Point3(6,20,30));
        CheckVector(Property(plan[2], "Position"), new Point3(40,50.5,60));
        CheckVector(Property(plan[3], "Position"), new Point3(39,50,60));
        CheckVector(Property(plan[0], "Scale"), new Point3(1,0.5,2));
        CheckVector(Property(plan[1], "Scale"), new Point3(2,4,1));
        CheckVector(Property(plan[2], "Scale"), new Point3(0.25,0.125,0.5));
        CheckVector(Property(plan[3], "Scale"), new Point3(0.5,1,0.25));
        CheckRotation(Property(plan[1], "Rotation"), 0.5f,0.5f,0.5f,0.5f);
        CheckVector(Get(parts[0], "position"), new Point3(1,0,0));
        CheckVector(Get(parts[1], "scale"), new Point3(1,2,0.5));
        Require((int)Get(session, "placementPlanIndex") == 0, "Scaling advanced placement queue");
        Console.WriteLine("PASS actual Session: 50%/200% whole-group scale changes rotated root offsets and every part scale; source/rotation/index preserved");
    }

    private static void TestUniformSingleScale()
    {
        object session = Session(0);
        Set(session, "activeBlueprint", null);
        object piece = RuntimeHelpers.GetUninitializedObject(pieceType);
        Set(session, "selectedPiece", piece);
        Require(Add(session, new Point3(1,2,3), Rotation(0,0,0,1), 0.01f), "Native single 1% rejected");
        Require(Add(session, new Point3(4,5,6), Rotation(0,0,0,1), 4f), "Native single 400% rejected");
        IList plan = (IList)Get(session, "placementPlan");
        IList sources = (IList)Get(session, "placementPlanPieces");
        CheckVector(Property(plan[0], "Position"), new Point3(1,2,3));
        CheckVector(Property(plan[0], "Scale"), new Point3(0.01,0.01,0.01));
        CheckVector(Property(plan[1], "Scale"), new Point3(4,4,4));
        Require(ReferenceEquals(sources[0], piece) && ReferenceEquals(sources[1], piece), "Single scale replaced native source");
        foreach (float invalid in new[] { 0f, -1f, 0.009f, 4.001f, float.NaN, float.PositiveInfinity })
        {
            Require(!Add(session, new Point3(7,8,9), Rotation(0,0,0,1), invalid), "Invalid single scale accepted: " + invalid);
            Require(plan.Count == 2 && sources.Count == 2, "Invalid single scale changed existing queue");
            Require(!string.IsNullOrEmpty((string)Get(session, "repeatPlanError")), "Invalid single scale has no arming error");
        }
        Console.WriteLine("PASS actual Session: native single receives uniform 1%/400% snapshot; invalid factors rejected without queue mutation");
    }

    private static void TestInvalidGroupScaleIsAtomic()
    {
        foreach (float factor in new[] { 0.5f, 2f, 0f, float.NaN, float.PositiveInfinity })
        {
            object session = Session(2);
            Require(Add(session, new Point3(10,20,30), Rotation(0,0,0,1)), "Valid baseline group rejected");
            IList plan = (IList)Get(session, "placementPlan");
            IList sources = (IList)Get(session, "placementPlanPieces");
            object first = plan[0], last = plan[1];
            // At 50%/200% the first part remains legal, while the second crosses 1%/400%.
            Require(!Add(session, new Point3(40,50,60), Rotation(0,0,0,1), factor), "Invalid scaled group accepted: " + factor);
            Require(plan.Count == 2 && sources.Count == 2 && first.Equals(plan[0]) && last.Equals(plan[1]),
                "Rejected scaled group partially appended or changed previous entries");
            Require(!string.IsNullOrEmpty((string)Get(session, "repeatPlanError")), "Invalid scaled group has no arming error");
            Require((int)Get(session, "placementPlanIndex") == 0, "Invalid scale advanced placement queue");
        }
        Console.WriteLine("PASS actual Session: later invalid part scale rejects whole group atomically with repeatPlanError; previous queue untouched");
    }

    private static object StoredVector(float x, float y, float z)
    {
        object value = Activator.CreateInstance(partType.GetField("position").FieldType);
        Set(value, "x", x); Set(value, "y", y); Set(value, "z", z);
        return value;
    }
    private static object Rotation(float x, float y, float z, float w) =>
        Activator.CreateInstance(rotationType, x,y,z,w);
    private static bool Add(object session, Point3 position, object rotation, float uniformScale = 1f) =>
        (bool)append.Invoke(session, new[] {
            Activator.CreateInstance(vectorType, (float)position.X,(float)position.Y,(float)position.Z), rotation, (object)uniformScale });
    private static object Get(object target, string name) => target.GetType().GetField(name, Instance).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Instance).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void CheckVector(object actual, Point3 expected)
    {
        Require(Math.Abs((float)Get(actual,"x") - expected.X) < 0.0001 &&
            Math.Abs((float)Get(actual,"y") - expected.Y) < 0.0001 &&
            Math.Abs((float)Get(actual,"z") - expected.Z) < 0.0001, "Composed vector differs from expected");
    }
    private static void CheckRotation(object actual, float x, float y, float z, float w)
    {
        CheckVector(actual, new Point3(x,y,z));
        Require(Math.Abs((float)Get(actual,"w") - w) < 0.0001, "Composed rotation differs from expected");
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}

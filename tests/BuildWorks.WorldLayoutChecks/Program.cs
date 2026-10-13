using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;

internal static class Program
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type sessionType, blueprintType, partType, vectorType, rotationType, pieceType;
    private static MethodInfo append, appendAtPivot;
    private static readonly GameObject[] fixtureObjects = new GameObject[2];

    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string[] folders = { Path.Combine(root, "artifacts", "bin"),
            Path.GetFullPath(Path.Combine(root, "..", "BepInEx", "core")),
            @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed" };
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            if (new AssemblyName(e.Name).Name.StartsWith("Unity", StringComparison.Ordinal)) return null;
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
            TestNativeBuildAvailability(asm);
            TestNativeMaterialVariation(asm);
            TestNativeMaterialState(asm);
            TestMaterialPreviewFailure(asm);
            TestMaterialSeedPlan();
            TestGridComposition();
            TestSignPayloadLifecycle();
            TestItemStandPayloadLifecycle();
            TestCustomPointPayloadLifecycle();
            TestPivotLayoutComposition();
            TestPlacementSupportRootComposition();
            TestHistorySurvivesPivotFrameSwitch();
            TestRotatedRoots();
            TestLimit();
            TestWorldCaptureScale();
            TestUniformGroupScale();
            TestUniformSingleScale();
            TestInvalidGroupScaleIsAtomic();
            TestPendingScaleIsAtomic();
            TestUnavailablePieceIsAtomic();
            TestPrimaryPartSelection();
            TestSinglePartBlueprint();
            Console.WriteLine("All actual Session root-composition checks passed; no game placement executed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            foreach (GameObject fixture in fixtureObjects)
                if (fixture) UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    private static void TestNativeBuildAvailability(Assembly plugin)
    {
        Type tableType = pieceType.Assembly.GetType("PieceTable", true);
        Type organizer = plugin.GetType("OstrixMods.BuildWorks.HammerCatalogOrganizer", true);
        MethodInfo available = sessionType.GetMethod("IsBuildPieceAvailable", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo signature = organizer.GetMethod("AvailabilitySignature", BindingFlags.Static | BindingFlags.Public);
        var root = new GameObject("Inert availability table"); root.SetActive(false);
        var roof = new GameObject("wood_wall_roof"); roof.SetActive(false);
        Component table = null;
        var allLists = (IDictionary)organizer.GetField("AllLists", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        long key = 0;
        try
        {
            table = root.AddComponent(tableType);
            object piece = roof.AddComponent(pieceType);
            Set(table, "m_pieces", new System.Collections.Generic.List<GameObject> { roof });
            object nativeSet = Get(table, "m_availablePieces");
            nativeSet.GetType().GetMethod("Add").Invoke(nativeSet, new[] { piece });
            Require((bool)available.Invoke(null, new[] { (object)table, piece }),
                "Native complete available set was hidden by empty/stale category caches");
            IList categories = (IList)Get(table, "m_availablePiecesByCategory");
            IList visible = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(pieceType));
            categories.Add(visible);
            Require((bool)organizer.GetMethod("EnsureVisible", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new[] { (object)table, piece }) && visible.Contains(piece),
                "An eligible native piece could not become visible before catalogue cache refresh");
            string before = (string)signature.Invoke(null, new object[] { table });
            key = (long)table.GetInstanceID() << 32;
            IList cached = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(pieceType));
            cached.Add(piece); allLists.Add(key, cached);
            nativeSet.GetType().GetMethod("Remove").Invoke(nativeSet, new[] { piece });
            Require(!(bool)available.Invoke(null, new[] { (object)table, piece }),
                "Stale category cache authorized a piece removed from native availability");
            Require((string)signature.Invoke(null, new object[] { table }) != before,
                "Registry signature ignored native availability change while category cache stayed stale");
            Require(!(bool)organizer.GetMethod("EnsureVisible", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new[] { (object)table, piece }) && visible.Count == 1,
                "EnsureVisible exposed a no-longer-available piece or changed UI after refusal");
            Console.WriteLine("PASS actual native PieceTable: complete set survives stale/empty UI lists; stale cache cannot grant access; signature tracks eligibility; unavailable visibility denied. Inert data only.");
        }
        finally
        {
            allLists.Remove(key);
            UnityEngine.Object.DestroyImmediate(roof); UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestMaterialSeedPlan()
    {
        object session = Session(2);
        IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
        Set(parts[0], "materialSeed", 0);
        Set(parts[1], "materialSeed", int.MaxValue);
        Require(Add(session, new Point3(0, 0, 0), Rotation(0, 0, 0, 1)) &&
            Add(session, new Point3(4, 0, 0), Rotation(0, 0, 0, 1)), "Seed repeat plan append failed");
        IList plan = (IList)Get(session, "placementPlan");
        for (int index = 0; index < plan.Count; ++index)
            Require((int)Property(plan[index], "MaterialSeed") == (index % 2 == 0 ? 0 : int.MaxValue),
                "Repeated placement plan lost or swapped material seed");
        Set(parts[0], "materialSeed", 1234);
        Require((int)Property(plan[0], "MaterialSeed") == 0, "Armed plan aliases mutable source material seed");
        plan.Clear(); ((IList)Get(session, "placementPlanPieces")).Clear();
        Set(parts[1], "materialSeed", null);
        Require(Add(session, new Point3(0, 0, 0), Rotation(0, 0, 0, 1)), "Updated seed plan append failed");
        Require((int)Property(plan[0], "MaterialSeed") == 1234 && Property(plan[1], "MaterialSeed") == null,
            "Plan confused unspecified, zero and changed material seed");
        Console.WriteLine("PASS actual Session: material seed zero/Max/null follows repeated parts without source aliasing; world guard remains separate");
    }

    private static void TestMaterialPreviewFailure(Assembly plugin)
    {
        object session = FormatterServices.GetUninitializedObject(sessionType);
        Set(session, "materialPreview", Activator.CreateInstance(plugin.GetType("OstrixMods.BuildWorks.NativePlacementMaterialPreview", true), true));
        MethodInfo update = sessionType.GetMethod("UpdateNativeMaterialPreview", Instance);
        var ghost = new GameObject("Inert invalid material preview");
        try
        {
            update.Invoke(session, new object[] { ghost, ghost, 0 });
            Require((bool)Get(session, "materialPreviewFailureShown"), "Preview failure was not contained/reported");
            update.Invoke(session, new object[] { ghost, ghost, 0 });
            Require((bool)Get(session, "materialPreviewFailureShown"), "Repeated preview failure escaped containment");
            update.Invoke(session, new object[] { ghost, ghost, null });
            Require(!(bool)Get(session, "materialPreviewFailureShown"), "Cleared material preview retained a stale failure");
            Console.WriteLine("PASS actual Session: invalid native preview is contained before creation; repeated failure and null reset leave native ghost intact");
        }
        finally { UnityEngine.Object.DestroyImmediate(ghost); }
    }

    private static void TestNativeMaterialVariation(Assembly plugin)
    {
        Type componentType = pieceType.Assembly.GetType("RandomMaterialValues", true);
        Type propertyType = componentType.GetNestedType("VectorVariationProperty", BindingFlags.Public);
        Type adapter = plugin.GetType("OstrixMods.BuildWorks.BlueprintMaterialVariation", true);
        MethodInfo apply = adapter.GetMethod("ApplyPreview", BindingFlags.NonPublic | BindingFlags.Static);
        var source = new GameObject("Native material source"); source.SetActive(false);
        var copy = new GameObject("Native material copy"); copy.SetActive(false);
        try
        {
            var scope = new GameObject("scope"); scope.transform.SetParent(source.transform, false);
            var visualScope = new GameObject("scope"); visualScope.transform.SetParent(copy.transform, false);
            Renderer renderer = visualScope.AddComponent<MeshRenderer>();
            Renderer sibling = copy.AddComponent<MeshRenderer>();
            object component = scope.AddComponent(componentType);
            object property = Activator.CreateInstance(propertyType);
            var minimum = new Vector4(.1f, .2f, .3f, .4f);
            var maximum = new Vector4(.6f, .7f, .8f, .9f);
            Set(property, "m_minimum", minimum); Set(property, "m_maximum", maximum);
            Set(property, "m_propertyNames", new System.Collections.Generic.List<string> { "_NativeVariation" });
            IList properties = (IList)Get(component, "m_vectorProperties");
            properties.Add(property); properties.Add(property);
            FieldInfo rng = propertyType.BaseType.GetField("m_random", Instance);
            Require(rng != null && rng.GetValue(property) == null, "Native variation source RNG is not pristine");
            var block = new MaterialPropertyBlock(); block.SetFloat("_Unrelated", 42); renderer.SetPropertyBlock(block);
            foreach (int seed in new[] { 0, 1234, int.MaxValue })
            {
                apply.Invoke(null, new object[] { source, copy, seed });
                renderer.GetPropertyBlock(block);
                var random = new System.Random(unchecked(seed + 1));
                var expected = new Vector4(
                    Mathf.Lerp(minimum.x, maximum.x, (float)random.NextDouble()),
                    Mathf.Lerp(minimum.y, maximum.y, (float)random.NextDouble()),
                    Mathf.Lerp(minimum.z, maximum.z, (float)random.NextDouble()),
                    Mathf.Lerp(minimum.w, maximum.w, (float)random.NextDouble()));
                Require(block.GetVector("_NativeVariation") == expected && block.GetFloat("_Unrelated") == 42,
                    "Native GetValue/seed index overflow or unrelated MPB preservation differs");
                Require(rng.GetValue(property) == null, "Adapter mutated native source descriptor RNG");
                sibling.GetPropertyBlock(block);
                Require(block.GetVector("_NativeVariation") == Vector4.zero, "Variation escaped native child scope");
            }
            try { apply.Invoke(null, new object[] { source, copy, -1 }); throw new Exception("Negative seed accepted"); }
            catch (TargetInvocationException error) { Require(error.InnerException is ArgumentException, "Wrong negative seed failure"); }
            Console.WriteLine("PASS actual host RandomMaterialValues.GetValue: zero/max seed, index wrap, owned RNG, nested renderer scope and unrelated MPB preserved; no world restore executed");
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(copy); }
    }

    private static void TestNativeMaterialState(Assembly plugin)
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Assembly host = pieceType.Assembly;
        Type state = plugin.GetType("OstrixMods.BuildWorks.BlueprintMaterialState", true);
        Type viewType = host.GetType("ZNetView", true), dataType = host.GetType("ZDO", true);
        Type idType = host.GetType("ZDOID", true), netType = host.GetType("ZNet", true);
        Type managerType = host.GetType("ZDOMan", true), randomType = host.GetType("RandomMaterialValues", true);
        Type materialManType = host.GetType("MaterialMan", true);
        FieldInfo materialInstance = materialManType.GetField("s_instance", Static);
        object oldMaterialMan = materialInstance.GetValue(null);
        FieldInfo netInstance = netType.GetField("m_instance", Static);
        FieldInfo server = netType.GetField("m_isServer", Static);
        FieldInfo managerInstance = managerType.GetField("s_instance", Static);
        FieldInfo init = viewType.GetField("m_initZDO", Static), useInit = viewType.GetField("m_useInitZDO", Static);
        object oldNet = netInstance.GetValue(null), oldServer = server.GetValue(null);
        object oldManager = managerInstance.GetValue(null), oldInit = init.GetValue(null), oldUse = useInit.GetValue(null);
        Require(oldNet == null && oldManager == null && oldInit == null && oldMaterialMan == null, "Material state fixture requires an isolated host");
        var roots = new System.Collections.Generic.List<GameObject>();
        GameObject materialRoot = null;
        var data = new System.Collections.Generic.List<object>();
        MethodInfo apply = state.GetMethod("ApplyCreated", Static), capture = state.GetMethod("TryCapture", Static);
        MethodInfo fresh = state.GetMethod("RequireFreshCreation", Static);
        MethodInfo canRestore = state.GetMethod("CanRestore", Static);
        MethodInfo sharedCleanup = sessionType.GetMethod("SharesNativePlacementData", Static);
        int key = (int)state.GetField("Key", Static).GetValue(null);
        uint nextId = 9000;
        object Data(uint? id = null)
        {
            object value = Activator.CreateInstance(dataType);
            Set(value, "m_uid", Activator.CreateInstance(idType, 1L, id ?? nextId++));
            dataType.GetProperty("Owner", Instance).SetValue(value, true);
            dataType.GetProperty("Valid", Instance).SetValue(value, true);
            data.Add(value);
            return value;
        }
        GameObject Root(string name, object value)
        {
            var root = new GameObject(name); root.SetActive(false); roots.Add(root);
            root.AddComponent(pieceType);
            object view = root.AddComponent(viewType); Set(view, "m_zdo", value);
            object random = root.AddComponent(randomType);
            Type propertyType = randomType.GetNestedType("VectorVariationProperty", BindingFlags.Public);
            object property = Activator.CreateInstance(propertyType);
            Set(property, "m_minimum", Vector4.zero); Set(property, "m_maximum", Vector4.one);
            Set(property, "m_propertyNames", new System.Collections.Generic.List<string> { "_NativeVariation" });
            ((IList)Get(random, "m_vectorProperties")).Add(property);
            return root;
        }
        int Read(object value) => (int)dataType.GetMethod("GetInt", new[] { typeof(int), typeof(int) })
            .Invoke(value, new object[] { key, -1 });
        void Reject(GameObject placed, GameObject source, GameObject ghost, int seed)
        {
            try { apply.Invoke(null, new object[] { placed, source, ghost, seed }); throw new Exception("Unsafe material write accepted"); }
            catch (TargetInvocationException error) { Require(error.InnerException is InvalidOperationException, "Wrong material refusal"); }
        }
        try
        {
            var network = new GameObject("Inert material network"); network.SetActive(false); roots.Add(network);
            netInstance.SetValue(null, network.AddComponent(netType)); server.SetValue(null, true);
            // Non-persistent data keeps native SetDirtySector away from world/save state.
            managerInstance.SetValue(null, FormatterServices.GetUninitializedObject(managerType));
            init.SetValue(null, null); useInit.SetValue(null, false);
            object sourceData = Data(), ghostData = Data(), placedData = Data();
            var source = Root("Material source", sourceData); var ghost = Root("Material ghost", ghostData);
            var placed = Root("Material placed", placedData);
            Renderer renderer = placed.AddComponent<MeshRenderer>();
            materialRoot = new GameObject("Inert native MaterialMan"); materialRoot.SetActive(false);
            object materialMan = materialRoot.AddComponent(materialManType);
            materialInstance.SetValue(null, materialMan);
            Set(materialMan, "m_propertyBlock", new MaterialPropertyBlock());
            var randomComponent = (MonoBehaviour)placed.GetComponent(randomType);
            // Inactive fixtures deliberately skip native Start's active-hierarchy lookup/scheduling.
            Set(randomComponent, "m_nview", placed.GetComponent(viewType));
            Set(randomComponent, "m_piece", placed.GetComponent(pieceType));
            Require(!(bool)sharedCleanup.Invoke(null, new object[] { placed, source }), "Fresh placement cannot be cleaned up");
            fresh.Invoke(null, null);
            foreach (int seed in new[] { 0, int.MaxValue })
            {
                apply.Invoke(null, new object[] { placed, source, ghost, seed });
                Require(Read(placedData) == seed && Read(sourceData) == -1 && Read(ghostData) == -1,
                    "Native material write changed source/ghost or lost zero/Max seed");
                Set(randomComponent, "m_randomSeed", -1); Set(randomComponent, "m_isSet", false);
                randomType.GetMethod("CheckMaterial", Instance).Invoke(randomComponent, null);
                materialManType.GetMethod("Update", Instance).Invoke(materialMan, null);
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                var expectedRandom = new System.Random(seed);
                var expected = new Vector4((float)expectedRandom.NextDouble(), (float)expectedRandom.NextDouble(),
                    (float)expectedRandom.NextDouble(), (float)expectedRandom.NextDouble());
                Require(block.GetVector("_NativeVariation") == expected && Read(placedData) == seed &&
                    (int)Get(randomComponent, "m_randomSeed") == seed && (bool)Get(randomComponent, "m_isSet"),
                    "Native CheckMaterial/MaterialMan did not retain the applied seed and appearance");
                object[] captured = { placed.GetComponent(pieceType), null };
                Require((bool)capture.Invoke(null, captured) && (int)captured[1] == seed, "Native capture differs from stored material seed");
                MethodInfo serialize = dataType.GetMethod("Serialize");
                Type packageType = serialize.GetParameters()[0].ParameterType;
                object package = Activator.CreateInstance(packageType), received = Data();
                serialize.Invoke(placedData, new[] { package });
                packageType.GetMethod("SetPos").Invoke(package, new object[] { 0 });
                dataType.GetMethod("Deserialize").Invoke(received, new[] { package });
                Require(Read(received) == seed && !Get(received, "m_uid").Equals(Get(placedData, "m_uid")),
                    "Native packet round-trip lost seed or replaced recipient identity");
            }
            uint revision = (uint)Property(placedData, "DataRevision");
            apply.Invoke(null, new object[] { placed, source, ghost, null });
            Require((uint)Property(placedData, "DataRevision") == revision, "Unspecified seed changed native state");
            Reject(placed, source, ghost, -1); Reject(source, source, ghost, 3); Reject(ghost, source, ghost, 3);
            dataType.GetProperty("Owner", Instance).SetValue(placedData, false); Reject(placed, source, ghost, 3);
            dataType.GetProperty("Owner", Instance).SetValue(placedData, true);
            Set(placed.GetComponent(viewType), "m_zdo", sourceData); Reject(placed, source, ghost, 3);
            Require((bool)sharedCleanup.Invoke(null, new object[] { placed, source }), "Cleanup would delete aliased source data");
            object alias = Data((uint)Property(Get(sourceData, "m_uid"), "ID"));
            Set(placed.GetComponent(viewType), "m_zdo", alias); Reject(placed, source, ghost, 3);
            Require((bool)sharedCleanup.Invoke(null, new object[] { placed, source }), "Cleanup missed equal source UID");
            Set(placed.GetComponent(viewType), "m_zdo", placedData);
            ((Behaviour)placed.GetComponent(randomType)).enabled = false; Reject(placed, source, ghost, 3);
            ((Behaviour)placed.GetComponent(randomType)).enabled = true;
            Set(placed.GetComponent(viewType), "m_zdo", null); Reject(placed, source, ghost, 3);
            Set(placed.GetComponent(viewType), "m_zdo", placedData);
            object originalId = Get(placedData, "m_uid");
            Set(placedData, "m_uid", Activator.CreateInstance(idType)); Reject(placed, source, ghost, 3);
            Set(placedData, "m_uid", originalId);
            foreach (bool withData in new[] { false, true })
            {
                init.SetValue(null, withData ? sourceData : null); useInit.SetValue(null, !withData);
                try { fresh.Invoke(null, null); throw new Exception("Existing ZDO initialization accepted"); }
                catch (TargetInvocationException error) { Require(error.InnerException is InvalidOperationException, "Wrong init-ZDO refusal"); }
            }
            Require(Read(sourceData) == -1 && Read(ghostData) == -1 && Read(placedData) == int.MaxValue &&
                (uint)Property(placedData, "DataRevision") == revision, "Rejected write mutated native state");
            object[] absent = { source.GetComponent(pieceType), null };
            Require(!(bool)capture.Invoke(null, absent) && absent[1] == null, "Capture invented an uninitialized material variant");
            var ordinary = new GameObject("Ordinary piece"); ordinary.SetActive(false); roots.Add(ordinary);
            object[] plain = { ordinary.AddComponent(pieceType), null };
            Require((bool)capture.Invoke(null, plain) && plain[1] == null, "Ordinary piece acquired a material seed");
            Require((bool)canRestore.Invoke(null, new object[] { ordinary, null }) &&
                !(bool)canRestore.Invoke(null, new object[] { ordinary, 0 }) &&
                (bool)canRestore.Invoke(null, new object[] { source, 0 }) &&
                (bool)canRestore.Invoke(null, new object[] { source, int.MaxValue }) &&
                !(bool)canRestore.Invoke(null, new object[] { source, -1 }), "Material readiness lost supported/null/unsupported distinctions");
            Console.WriteLine("PASS actual host material state: native Set/Get and in-memory packet round-trip, zero/Max, capture, null no-op, owner/identity/init-ZDO guards; inert data only, no placement/save/network run");
            Console.WriteLine("PASS actual host CheckMaterial/MaterialMan: applied zero/Max seed reaches renderer block without replacement; explicit inactive bindings, no Start/frame scheduling/game lifecycle proof");
        }
        finally
        {
            init.SetValue(null, oldInit); useInit.SetValue(null, oldUse);
            foreach (GameObject root in roots)
            {
                Component view = root.GetComponent(viewType);
                if (view) Set(view, "m_zdo", null);
                UnityEngine.Object.DestroyImmediate(root);
            }
            foreach (object value in data) dataType.GetMethod("Reset").Invoke(value, null);
            if (materialRoot) UnityEngine.Object.DestroyImmediate(materialRoot);
            materialInstance.SetValue(null, oldMaterialMan);
            netInstance.SetValue(null, oldNet); server.SetValue(null, oldServer); managerInstance.SetValue(null, oldManager);
        }
    }

    private static void TestSinglePartBlueprint()
    {
        object session = Session(1);
        Set(Get(session, "activeBlueprint"), "primaryPartId", "part-0");
        float s = (float)Math.Sqrt(.5);
        Require(Add(session, new Point3(10, 20, 30), Rotation(0, 0, s, s)), "Single blueprint expansion failed");
        IList plan = (IList)Get(session, "placementPlan");
        Require(plan.Count == 1 && ((IList)Get(session, "placementPlanPieces")).Count == 1,
            "Single blueprint synthesized a second placement");
        CheckVector(Property(plan[0], "Position"), new Point3(10, 21, 30));
        CheckVector(Property(plan[0], "Scale"), new Point3(2, .5, 1.25));
        CheckRotation(Property(plan[0], "Rotation"), 0, 0, s, s);
        Console.WriteLine("PASS actual Session: one source part produces one native placement with composed rotation/scale/position");
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

    private static void TestPlacementSupportRootComposition()
    {
        float s = (float)Math.Sqrt(0.5);
        object supportPosition = Activator.CreateInstance(vectorType, 1f, 2f, 3f);
        object framePosition = Activator.CreateInstance(vectorType, 10f, 20f, 30f);
        foreach (float factor in new[] { 0.5f, 1f, 2f })
        {
            // Known root from support p=(1,2,3), q=Ry(90), target=(10,20,30)/Rz(90).
            // Quaternion.Inverse is native: the shared pose resolver is tested in Unity.
            object rootPosition = Activator.CreateInstance(vectorType,
                10 + 2 * factor, 20 + 3 * factor, 30 - factor);
            object rootRotation = Rotation(0.5f, -0.5f, 0.5f, 0.5f);
            object session = Session(2);
            IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
            foreach (object part in parts) Set(part, "scale", StoredVector(1, 1, 1));
            Require((bool)append.Invoke(session, new[] { rootPosition, rootRotation, (object)factor }),
                "Rotated support root append failed");
            IList plan = (IList)Get(session, "placementPlan");
            CheckVector(Property(plan[0], "Position"), new Point3(10 + 2 * factor, 20 + 3 * factor, 30));
            CheckVector(Property(plan[1], "Position"), new Point3(10, 20 + 3 * factor, 30 - factor));
            CheckRotation(Property(plan[1], "Rotation"), s, 0, s, 0);
            foreach (object part in parts) CheckVector(Get(part, "scale"), new Point3(1, 1, 1));
            if (factor != 1f) continue;
            Set(session, "blueprintEditPartIndex", -1);
            Set(session, "blueprintPivotLocal", supportPosition);
            Set(session, "currentPosition", framePosition);
            Set(session, "currentRotation", rootRotation);
            sessionType.GetMethod("CommitBlueprintEditTarget", Instance).Invoke(session, null);
            CheckVector(Get(session, "blueprintRootPosition"), new Point3(12, 23, 29));
            CheckRotation(Get(session, "blueprintRootRotation"), 0.5f, -0.5f, 0.5f, 0.5f);
            object snapshot = sessionType.GetMethod("CaptureHistorySnapshot", Instance).Invoke(session, null);
            CheckVector(Property(snapshot, "Position"), new Point3(12, 23, 29));
            Set(session, "blueprintPivotLocal", Activator.CreateInstance(vectorType, 0f, 2f, 0f));
            sessionType.GetMethod("ApplyHistorySnapshot", Instance).Invoke(session, new[] { snapshot });
            CheckVector(Get(session, "currentPosition"), new Point3(10, 23, 29));
            CheckRotation(Get(session, "currentRotation"), 0.5f, -0.5f, 0.5f, 0.5f);
        }
        Console.WriteLine("PASS actual Session: known rotated support roots at 50/100/200% scale, source poses unchanged, commit/history preserve geometry root across pivot change; native pose resolver checked separately in Unity");
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

    private static void TestSignPayloadLifecycle()
    {
        object session = Session(2);
        IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
        Set(parts[0], "signText", "Дом\n<b>Ostrix</b>");
        Set(parts[1], "signText", "");
        Require(Add(session, new Point3(0,0,0), Rotation(0,0,0,1)), "Sign root append failed");
        Require(Add(session, new Point3(5,0,0), Rotation(0,0,0,1)), "Sign repeat append failed");
        IList plan = (IList)Get(session, "placementPlan");
        for (int index = 0; index < plan.Count; ++index)
            Require((string)Property(plan[index], "SignText") == (index % 2 == 0 ? "Дом\n<b>Ostrix</b>" : ""),
                "Repeat sign payload follows the wrong part");
        Type objectType = sessionType.GetField("pendingNativePiece", Instance).FieldType;
        object created = ((Component)NativePiece(0)).gameObject;
        Require(objectType.IsInstanceOfType(created), "Pending native piece has an unexpected host type");
        MethodInfo take = sessionType.GetMethod("TakePendingPlacementSign", Instance);
        foreach (bool succeeded in new[] { false, true })
        {
            Set(session, "pendingNativePiece", created);
            Set(session, "pendingNativePlanIndex", 2);
            Set(session, "placementPlanIndex", 2);
            object[] result = { succeeded, null, null };
            Require((bool)take.Invoke(session, result) == succeeded, "Native rejection applied sign payload");
            Require(!succeeded || ReferenceEquals(result[1], created) && (string)result[2] == "Дом\n<b>Ostrix</b>",
                "Native success returned a different object or text");
            Require(Get(session, "pendingNativePiece") == null && (int)Get(session, "pendingNativePlanIndex") == -1,
                "Pending sign payload survived completion/rejection");
            Require(!(bool)take.Invoke(session, new object[] { true, null, null }), "Sign payload applied twice");
        }
        Set(session, "pendingNativePiece", created);
        Set(session, "pendingNativePlanIndex", 1);
        Require(!(bool)take.Invoke(session, new object[] { true, null, null }), "Stale previous step applied text");
        Set(session, "pendingNativePiece", created);
        Set(session, "pendingNativePlanIndex", 3);
        Set(session, "placementPlanIndex", 3);
        object[] empty = { true, null, null };
        Require((bool)take.Invoke(session, empty) && (string)empty[2] == "", "Explicit blank sign lost its payload");
        Console.WriteLine("PASS actual Session: sign text follows repeated steps; native rejection/stale steps consume without apply; success/blank text are one-shot");
    }

    private static void TestItemStandPayloadLifecycle()
    {
        object session = Session(2);
        IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
        Type dataType = partType.GetField("foreignData", Instance).FieldType;
        object data = Activator.CreateInstance(dataType, true);
        Set(data, "format", BlueprintForeignPartData.NativeItemStandFormat);
        Set(data, "info", "SwordFixture:2:3:1");
        Set(parts[1], "foreignData", data);
        Require(Add(session, new Point3(0,0,0), Rotation(0,0,0,1)) &&
            Add(session, new Point3(4,0,0), Rotation(0,0,0,1)), "Mount repeated root append failed");
        IList plan = (IList)Get(session, "placementPlan");
        for (int index = 0; index < plan.Count; ++index)
        {
            var contents = Property(plan[index], "ItemStandContents") as BlueprintForeignPartData;
            Require(index % 2 == 0 ? contents == null : contents?.Info == "SwordFixture:2:3:1",
                "Mount payload attached to the wrong repeated part");
        }
        Set(data, "info", "Changed:0:1:0");
        Require(((BlueprintForeignPartData)Property(plan[1], "ItemStandContents")).Info == "SwordFixture:2:3:1",
            "Armed plan aliases mutable source holder metadata");
        Set(session, "pendingItemStandContents", Property(plan[1], "ItemStandContents"));
        Set(session, "allowNativeGhostUpdate", true);
        sessionType.GetMethod("OnNativePlacementFinished", Instance).Invoke(session, new object[] { null, null, false });
        Require((bool)Get(session, "allowNativeGhostUpdate") && (int)Get(session, "placementPlanIndex") == 0,
            "Blocked repeat changed the pending mount stage");
        Console.WriteLine("PASS actual Session: repeated mounts retain immutable per-part payload; blocked native repeat leaves pending mount unchanged");
    }

    private static void TestCustomPointPayloadLifecycle()
    {
        object session = Session(2);
        var points = new[] { new Point3(0.25, -0.5, 2) };
        Set(session, "blueprintSnapLocalSets", new[] { points, null });
        Require(Add(session, new Point3(0,0,0), Rotation(0,0,0,1)) &&
            Add(session, new Point3(4,2,0), Rotation(0,0,1,0)), "Custom-point repeated root append failed");
        IList plan = (IList)Get(session, "placementPlan");
        for (int index = 0; index < plan.Count; ++index)
            Require(index % 2 == 0
                ? ReferenceEquals(Property(plan[index], "CustomSnapPoints"), points)
                : Property(plan[index], "CustomSnapPoints") == null,
                "Custom piece-local points were lost or assigned to another repeated part");
        Console.WriteLine("PASS actual Session: custom piece-local snap payload follows the owning part across differently rotated repeated roots");
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
        object session = Session(5000);
        for (int index = 0; index < 10; ++index)
            Require(Add(session, new Point3(index,0,0), Rotation(0,0,0,1)), "Valid 50000-part world plan failed");
        IList plan = (IList)Get(session, "placementPlan");
        object last = plan[49999];
        Require(!Add(session, new Point3(10,0,0), Rotation(0,0,0,1)), "Oversized plan accepted");
        Require(plan.Count == 50000 && ((IList)Get(session, "placementPlanPieces")).Count == 50000 &&
            last.Equals(plan[49999]), "Rejected group was partially appended or changed earlier entries");
        Require((bool)Get(session, "expandedPlanLimited"), "Oversized plan did not block arming");
        Require((int)Get(session, "placementPlanIndex") == 0, "Limit check advanced native placement index");
        Console.WriteLine("PASS actual Session: 50000 actual world parts allowed; next whole group rejected atomically and arming guard set");
    }

    private static object Session(int partCount)
    {
        object session = FormatterServices.GetUninitializedObject(sessionType);
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
            sources.Add(NativePiece(index % 2));
        return session;
    }

    private static object NativePiece(int index)
    {
        if (!fixtureObjects[index])
        {
            var fixture = new GameObject("BuildWorks inert layout fixture " + index);
            fixture.SetActive(false); // Prevent host Awake from initializing networking or world state.
            fixtureObjects[index] = fixture;
            Type viewType = pieceType.Assembly.GetType("ZNetView", true);
            object view = fixture.AddComponent(viewType);
            Set(view, "m_syncInitialScale", true);
            fixture.AddComponent(pieceType);
            Require(fixture.GetComponent(pieceType) && fixture.GetComponent(viewType) && !fixture.activeInHierarchy,
                "Inert host components were initialized or destroyed");
        }
        return fixtureObjects[index].GetComponent(pieceType);
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
        value = method.Invoke(null, new[] { Activator.CreateInstance(vectorType, 10f, 20f, 5f),
            Activator.CreateInstance(vectorType, 1f, 2f, .5f) });
        CheckVector(value, new Point3(10, 10, 10));
        foreach (float invalid in new[] { 0f, 0.009f, 10.001f, float.NaN, float.PositiveInfinity })
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
        object piece = NativePiece(0);
        Set(session, "selectedPiece", piece);
        Require(Add(session, new Point3(1,2,3), Rotation(0,0,0,1), 0.01f), "Native single 1% rejected");
        Require(Add(session, new Point3(4,5,6), Rotation(0,0,0,1), 4f), "Native single 400% rejected");
        Require(Add(session, new Point3(4,5,6), Rotation(0,0,0,1), 10f), "Native single 1000% rejected");
        IList plan = (IList)Get(session, "placementPlan");
        IList sources = (IList)Get(session, "placementPlanPieces");
        CheckVector(Property(plan[0], "Position"), new Point3(1,2,3));
        CheckVector(Property(plan[0], "Scale"), new Point3(0.01,0.01,0.01));
        CheckVector(Property(plan[1], "Scale"), new Point3(4,4,4));
        CheckVector(Property(plan[2], "Scale"), new Point3(10,10,10));
        Require(ReferenceEquals(sources[0], piece) && ReferenceEquals(sources[1], piece), "Single scale replaced native source");
        foreach (float invalid in new[] { 0f, -1f, 0.009f, 10.001f, float.NaN, float.PositiveInfinity })
        {
            Require(!Add(session, new Point3(7,8,9), Rotation(0,0,0,1), invalid), "Invalid single scale accepted: " + invalid);
            Require(plan.Count == 3 && sources.Count == 3, "Invalid single scale changed existing queue");
            Require(!string.IsNullOrEmpty((string)Get(session, "repeatPlanError")), "Invalid single scale has no arming error");
        }
        Console.WriteLine("PASS actual Session: native single receives uniform 1%/400%/1000% snapshot; invalid factors rejected without queue mutation");
    }

    private static void TestInvalidGroupScaleIsAtomic()
    {
        foreach (float factor in new[] { 0.5f, 2f, 0f, float.NaN, float.PositiveInfinity })
        {
            object session = Session(2);
            IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
            Set(parts[1], "scale", StoredVector(.01f, 10f, 1f));
            Require(Add(session, new Point3(10,20,30), Rotation(0,0,0,1)), "Valid baseline group rejected");
            IList plan = (IList)Get(session, "placementPlan");
            IList sources = (IList)Get(session, "placementPlanPieces");
            object first = plan[0], last = plan[1];
            // At 50%/200% the first part remains legal, while the second crosses 1%/1000%.
            Require(!Add(session, new Point3(40,50,60), Rotation(0,0,0,1), factor), "Invalid scaled group accepted: " + factor);
            Require(plan.Count == 2 && sources.Count == 2 && first.Equals(plan[0]) && last.Equals(plan[1]),
                "Rejected scaled group partially appended or changed previous entries");
            Require(!string.IsNullOrEmpty((string)Get(session, "repeatPlanError")), "Invalid scaled group has no arming error");
            Require((int)Get(session, "placementPlanIndex") == 0, "Invalid scale advanced placement queue");
        }
        Console.WriteLine("PASS actual Session: later invalid part scale rejects whole group atomically with repeatPlanError; previous queue untouched");
    }

    private static void TestPendingScaleIsAtomic()
    {
        object session = Session(2);
        Require(Add(session, new Point3(10,20,30), Rotation(0,0,0,1)), "Baseline group rejected");
        IList plan = (IList)Get(session, "placementPlan"), sources = (IList)Get(session, "placementPlanPieces");
        object first = plan[0], last = plan[1];
        IList parts = (IList)Get(Get(session, "activeBlueprint"), "parts");
        Set(parts[1], "scale", StoredVector(1, 1, 1)); // Numerically legal but semantically unresolved.
        Set(parts[1], "scaleIsAbsolute", true);
        Require(!Add(session, new Point3(40,50,60), Rotation(0,0,0,1)), "Pending absolute basis treated as native multiplier");
        Require(plan.Count == 2 && sources.Count == 2 && first.Equals(plan[0]) && last.Equals(plan[1]) &&
            (int)Get(session, "placementPlanIndex") == 0 && !string.IsNullOrEmpty((string)Get(session, "repeatPlanError")),
            "Pending group changed existing placement queue or failed silently");
        Console.WriteLine("PASS actual Session: unresolved absolute basis rejects whole group before planning, queue unchanged");
    }

    private static void TestUnavailablePieceIsAtomic()
    {
        object session = Session(2);
        Require(Add(session, new Point3(), Rotation(0,0,0,1)), "Baseline group rejected");
        IList plan = (IList)Get(session, "placementPlan");
        IList sources = (IList)Get(session, "activeBlueprintPieces");
        sources[1] = null;
        Require(!Add(session, new Point3(10,0,0), Rotation(0,0,0,1)) && plan.Count == 2,
            "Unavailable piece partially appended a group");
        sources[1] = NativePiece(1);
        Type viewType = pieceType.Assembly.GetType("ZNetView", true);
        object view = fixtureObjects[1].GetComponent(viewType);
        Set(view, "m_syncInitialScale", false);
        Require(!Add(session, new Point3(10,0,0), Rotation(0,0,0,1)) && plan.Count == 2,
            "Non-persistable scale partially appended a group");
        Set(view, "m_syncInitialScale", true);
        Console.WriteLine("PASS actual Session: missing Piece and disabled native scale persistence reject whole group atomically");
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

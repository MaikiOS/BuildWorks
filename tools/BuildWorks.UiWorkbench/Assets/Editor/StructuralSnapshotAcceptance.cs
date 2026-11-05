using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.LowLevelPhysics;
using Unity.Collections;
using Evaluation = OstrixMods.BuildWorks.Geometry.StructuralSupportEvaluation;
using Object = UnityEngine.Object;

internal static class StructuralSnapshotAcceptance
{
    internal static IEnumerable Run()
    {
        VerifyContactMeshReads();
        VerifyNativeTriangleWitnessPrecision();
        var source = new GameObject("StructuralScalarFixture"); source.SetActive(false); source.layer = 8;
        var box = source.AddComponent<BoxCollider>(); box.center = Vector3.up * .5f;
        var native = source.AddComponent<WearNTear>(); native.m_comOffset = Vector3.up * .5f;
        var staticSource = new GameObject("StructuralStaticFixture"); staticSource.SetActive(false);
        staticSource.AddComponent<BoxCollider>().center = Vector3.up * .5f;
        var snapshot = new BlueprintStructuralSnapshot();
        var extraSources = new List<GameObject>();
        BlueprintStructuralSnapshot dense = null;
        var contacts = new List<BlueprintStructuralSnapshot.Contact>();
        try
        {
            var lower = snapshot.Add("base", source, Vector3.zero, Quaternion.identity, Vector3.one);
            var upper = snapshot.Add("upper", source, Vector3.up, Quaternion.identity, Vector3.one);
            Require(lower.UnknownReason == null && lower.Profile.HasValue && lower.Relays && lower.NoSupportWear,
                "Fixture scalar profile was not captured");
            Require(lower.Colliders[0].gameObject.layer == source.layer &&
                lower.Root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 &&
                lower.Root.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                "Snapshot changed native layers or cloned gameplay components");
            Require(snapshot.TryContacts(lower, 0, contacts, out bool ground, out _) && ground,
                "Geometric contact with assumed Y=0 was missed");
            Require(snapshot.TryContacts(upper, 0, contacts, out ground, out _) && !ground &&
                contacts.Count == 1 && contacts[0].Source == lower,
                "Native envelopes did not find the existing lower part");
            var evaluated = Evaluation.EvaluateReceiver(upper.Profile.Value, Point(upper.Com), upper.ForceCorrectCom,
                ground, contacts.Select(contact => new Evaluation.Contact(Point(contact.Source.Origin),
                    Point(contact.Source.Com), Point(contact.Point), lower.Profile.Value.Maximum, contact.Source.Relays)).ToArray());
            Require(Mathf.Abs(evaluated.Support - 86.25f) < .001f && evaluated.HasSupport,
                "Snapshot contact and receiver math disagree for stacked boxes");

            var hovering = snapshot.Add("hovering", source, new Vector3(10, .7f, 0), Quaternion.identity, Vector3.one);
            Require(snapshot.TryContacts(hovering, 0, contacts, out ground, out _) && !ground && contacts.Count == 0,
                "A floating origin/half-meter zone became ground support");
            native.m_supports = false;
            snapshot.Add("noRelay", source, new Vector3(20, 0, 0), Quaternion.identity, Vector3.one);
            native.m_supports = true;
            var aboveChest = snapshot.Add("aboveNoRelay", source, new Vector3(20, 1, 0), Quaternion.identity, Vector3.one);
            Require(snapshot.TryContacts(aboveChest, 0, contacts, out ground, out _) && !ground && contacts.Count == 0,
                "A non-relaying piece supplied structural support");

            snapshot.Add("static", staticSource, new Vector3(30, 2, 0), Quaternion.identity, Vector3.one);
            var onStatic = snapshot.Add("onStatic", source, new Vector3(30, 3, 0), Quaternion.identity, Vector3.one);
            Require(snapshot.TryContacts(onStatic, -10, contacts, out ground, out _) && ground,
                "Verified static/no-WearNTear collider was not an intrinsic maximum-support anchor");

            native.FixtureMaximum = float.NaN;
            var unknown = snapshot.Add("unknown", source, new Vector3(40, 0, 0), Quaternion.identity, Vector3.one);
            native.FixtureMaximum = 100;
            Require(!snapshot.TryContacts(unknown, 0, contacts, out ground, out string reason) && !ground && reason != null,
                "Unknown material was painted as grounded/stable");
            var missing = snapshot.Add("missing", null, new Vector3(50, 0, 0), Quaternion.identity, Vector3.one);
            Require(!snapshot.TryContacts(missing, 0, contacts, out ground, out reason) && reason == "missing-prefab",
                "Missing prefab gained physical support");

            var child = new GameObject("OffsetShape"); child.transform.SetParent(source.transform, false);
            child.transform.localPosition = new Vector3(2, 0, 0); child.transform.localScale = new Vector3(.5f, 2, .25f);
            child.AddComponent<BoxCollider>().center = Vector3.up * .5f;
            native.m_forceCorrectCOMCalculation = true;
            var scaled = snapshot.Add("scaled", source, new Vector3(60, 3, 0), Quaternion.Euler(0, 90, 0), new Vector3(2, 3, 4));
            Require(Vector3.Distance(scaled.Com, new Vector3(60, 3.5f, 0)) < .0001f && scaled.ForceCorrectCom,
                "COM offset was incorrectly scaled or receiver flag lost");
            BoxCollider copiedChild = scaled.Colliders.OfType<BoxCollider>().Single(value => value.transform != scaled.Root.transform);
            Require(Vector3.Distance(copiedChild.transform.position, new Vector3(60, 3, -4)) < .001f &&
                Vector3.Distance(copiedChild.transform.lossyScale, new Vector3(1, 6, 1)) < .001f,
                "Nested transforms/scale were flattened in collider snapshot");
            Object.DestroyImmediate(child); native.m_forceCorrectCOMCalculation = false;

            GameObject ShapeSource(string name)
            {
                var item = new GameObject(name); item.SetActive(false); item.AddComponent<WearNTear>();
                extraSources.Add(item); return item;
            }
            var sphereSource = ShapeSource("StructuralSphere");
            sphereSource.AddComponent<SphereCollider>().center = Vector3.up * .5f;
            var spherePart = snapshot.Add("sphere", sphereSource, new Vector3(70, 0, 0), Quaternion.identity, new Vector3(2, 1, .5f));
            Require(spherePart.UnknownReason == null && snapshot.TryContacts(spherePart, 0, contacts, out ground, out _) && ground &&
                Mathf.Abs(spherePart.Colliders[0].bounds.size.y - 2) < .001f, "Native sphere scale/envelope was not preserved");
            var capsuleSource = ShapeSource("StructuralCapsule");
            capsuleSource.AddComponent<CapsuleCollider>().height = 2;
            var capsulePart = snapshot.Add("capsule", capsuleSource, new Vector3(80, 0, 0), Quaternion.identity, Vector3.one);
            Require(capsulePart.UnknownReason == null && snapshot.TryContacts(capsulePart, 0, contacts, out ground, out _) && ground,
                "Capsule native envelope did not meet assumed ground");
            GameObject meshFixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh shapeMesh = meshFixture.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(meshFixture);
            var meshSource = ShapeSource("StructuralMesh"); meshSource.AddComponent<MeshCollider>().sharedMesh = shapeMesh;
            var meshBase = snapshot.Add("meshBase", meshSource, new Vector3(90, .5f, 0), Quaternion.identity, Vector3.one);
            var meshTop = snapshot.Add("meshTop", meshSource, new Vector3(90, 1.5f, 0), Quaternion.identity, Vector3.one);
            Require(snapshot.TryContacts(meshTop, 0, contacts, out ground, out _) && !ground && contacts.Count == 1 &&
                contacts[0].Source == meshBase && Mathf.Abs(contacts[0].Point.y - 1) < .001f,
                "Non-convex mesh snapshot lost native overlap/down-ray contact");

            dense = new BlueprintStructuralSnapshot();
            var denseReceiver = dense.Add("receiver", source, Vector3.zero, Quaternion.identity, Vector3.one);
            for (int i = 0; i < 130; ++i) dense.Add("overlap" + i, source, Vector3.zero, Quaternion.identity, Vector3.one);
            Require(!dense.TryContacts(denseReceiver, -100, contacts, out ground, out reason) &&
                !ground && reason == "native-query-capacity", "Saturated native query was declared complete");
            Require(!dense.TryContacts(denseReceiver, 0, contacts, out ground, out reason) &&
                !ground && reason == "native-query-capacity", "Assumed ground hid a saturated native query");
            bool wrongOwnerRejected = false;
            try { snapshot.TryContacts(denseReceiver, 0, contacts, out _, out _); }
            catch (ArgumentException) { wrongOwnerRejected = true; }
            Require(wrongOwnerRejected, "Snapshot accepted another scene's receiver");
            bool badRotationRejected = false;
            try { snapshot.Add("badRotation", source, Vector3.zero, new Quaternion(float.MaxValue, 0, 0, 0), Vector3.one); }
            catch (ArgumentOutOfRangeException) { badRotationRejected = true; }
            Require(badRotationRejected, "Snapshot accepted a quaternion with overflowing length");
            Require(!source.activeSelf && source.transform.position == Vector3.zero &&
                source.transform.localScale == Vector3.one && native.m_supports && native.FixtureMaximum == 100,
                "Structural snapshot changed its source prefab");
            snapshot.Dispose(); snapshot.Dispose();
            bool rejected = false;
            try { snapshot.Add("afterDispose", source, Vector3.zero, Quaternion.identity, Vector3.one); }
            catch (ObjectDisposedException) { rejected = true; }
            Require(rejected, "Disposed snapshot accepted new geometry");
        }
        finally
        {
            snapshot.Dispose(); dense?.Dispose(); Object.DestroyImmediate(source); Object.DestroyImmediate(staticSource);
            foreach (GameObject item in extraSources) Object.DestroyImmediate(item);
        }
        while (snapshot.Unloading != null && !snapshot.Unloading.isDone) yield return null;
        while (dense?.Unloading != null && !dense.Unloading.isDone) yield return null;
        Require(!snapshot.IsSceneLoaded && (dense == null || !dense.IsSceneLoaded), "Snapshot physics scene leaked after disposal");
        Debug.Log("BUILDWORKS_STRUCTURAL_SNAPSHOT_OK fixture scalars; not native Valheim profile parity");
        foreach (object step in VerifyFinalPoseContact()) yield return step;
        foreach (object step in VerifySurfaceAdapter()) yield return step;
        foreach (object step in ObserveInactiveShapeQueries()) yield return step;
    }

    private static IEnumerable VerifySurfaceAdapter()
    {
        const int layer = 8;
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        var adapter = new BlueprintSurfaceContact();
        var owned = new List<GameObject>();
        var ownedMeshes = new List<Mesh>();
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cube = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive);
        Exception error = null;
        try
        {
            int index = 0;
            foreach (float coordinate in new[] { 101f, 12000f })
              foreach (string targetKind in new[] { "Box", "NonconvexMesh", "Sphere", "Capsule", "ConvexMesh" })
                foreach (string kind in new[] { "Box", "Sphere", "Capsule", "ConvexMesh", "NonconvexMesh" })
                {
                    var source = new GameObject("AdapterInactive_" + kind); owned.Add(source); source.SetActive(false);
                    Vector3 origin = new Vector3(coordinate + index++ * 8, coordinate, coordinate);
                    source.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 0, 90));
                    source.transform.localScale = new Vector3(2, 1, .5f);
                    var child = new GameObject("OffsetShape"); child.transform.SetParent(source.transform, false);
                    child.transform.localPosition = new Vector3(.25f, .5f, .3f);
                    Vector3 localCenter = new Vector3(.2f, .1f, 0);
                    Collider shape;
                    if (kind == "Box") { var c = child.AddComponent<BoxCollider>(); c.center = localCenter; shape = c; }
                    else if (kind == "Sphere") { var c = child.AddComponent<SphereCollider>(); c.center = localCenter; shape = c; }
                    else if (kind == "Capsule")
                    { var c = child.AddComponent<CapsuleCollider>(); c.radius = .5f; c.height = 4; c.direction = 1; c.center = localCenter; shape = c; }
                    else
                    { child.transform.localPosition += localCenter; var c = child.AddComponent<MeshCollider>(); c.sharedMesh = cube; c.convex = kind == "ConvexMesh"; shape = c; }
                    Require(BlueprintSurfaceContact.NeedsAdapter(source) == (kind != "Box"), "Surface adapter selector disagrees with actual receiver shapes");
                    Vector3 Center() => child.transform.TransformPoint(shape is MeshCollider ? Vector3.zero : localCenter);
                    var target = new GameObject("AdapterWorldTarget") { layer = layer }; owned.Add(target);
                    Collider support;
                    if (targetKind == "Sphere") support = target.AddComponent<SphereCollider>();
                    else if (targetKind == "Capsule")
                    { var c = target.AddComponent<CapsuleCollider>(); c.radius = .5f; c.height = 2; c.direction = 0; support = c; }
                    else if (targetKind == "Box") support = target.AddComponent<BoxCollider>();
                    else { var c = target.AddComponent<MeshCollider>(); c.sharedMesh = cube; c.convex = targetKind == "ConvexMesh"; support = c; }
                    target.transform.position = Center() + Vector3.up * 1.5f;
                    bool Query(out RaycastHit hit, out PlacementContactFailure failure, float range = 10) =>
                        adapter.TryFind(source, 1 << layer, Center() + Vector3.back * 5, range, out hit, out failure);
                    bool found = Query(out var hit, out var failure);
                    {
                        Require(found && failure == PlacementContactFailure.None && hit.collider == support &&
                            Mathf.Abs(hit.point.y - (Center().y + 1)) < .002f, "Adapter final-pose witness missing: " + kind + "/" + targetKind + ": " + failure);
                        target.transform.position -= Vector3.up * .1f;
                        Require(Query(out hit, out failure) && failure == PlacementContactFailure.None && hit.collider == support,
                            "Adapter rejected actual overlapping shapes: " + kind + "/" + targetKind + ": " + failure);
                        target.transform.position += Vector3.up * .1f;
                        int cachedCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
                        source.transform.position += Vector3.right * 3;
                        child.transform.localPosition += new Vector3(.15f, .2f, 0);
                        target.transform.position = Center() + Vector3.up * 1.5f;
                        Require(Query(out hit, out failure) && hit.collider == support &&
                            Mathf.Abs(hit.point.y - (Center().y + 1)) < .002f &&
                            UnityEngine.SceneManagement.SceneManager.sceneCount == cachedCount,
                            "Cached adapter lost changed root/child pose or created another scene: " + kind);
                        Require(!Query(out _, out failure, .25f) && (failure & PlacementContactFailure.OutOfRange) != 0,
                            "Adapter ignored native placement range: " + kind);
                        target.transform.position += Vector3.up * .01f;
                        Require(!Query(out _, out failure), "Adapter bridged a1cm gap: " + kind);
                        target.transform.position -= Vector3.up * .01f;
                        target.SetActive(false);
                        support.enabled = false;
                        var trigger = target.AddComponent<BoxCollider>(); trigger.isTrigger = true;
                        target.SetActive(true);
                        Require(trigger.isTrigger && !support.enabled && !Query(out _, out _), "Adapter accepted a legal trigger: " + kind);
                        target.SetActive(false); Object.DestroyImmediate(trigger); support.enabled = true;
                        var body = target.AddComponent<Rigidbody>(); body.isKinematic = true;
                        target.SetActive(true);
                        Require(!Query(out _, out failure) && (failure & PlacementContactFailure.Obstructed) != 0,
                            "Adapter accepted a rigidbody surface: " + kind);
                        target.SetActive(false); Object.DestroyImmediate(body); target.SetActive(true);
                        if (shape is SphereCollider sphere)
                        {
                            sphere.radius = .7f;
                            target.transform.position = Center() + Vector3.up * 1.9f;
                            Require(Query(out hit, out _) && hit.collider == support, "Cached sphere radius was stale");
                        }
                        if (shape is CapsuleCollider capsule)
                        {
                            capsule.direction = 0;
                            // Independent engine oracle: configure an inactive collider before activation, without the snapshot factory.
                            var control = new GameObject("IndependentCapsuleAxisControl"); owned.Add(control); control.SetActive(false);
                            control.transform.rotation = source.transform.rotation; control.transform.localScale = source.transform.lossyScale;
                            var controlChild = new GameObject("IndependentOffset"); controlChild.transform.SetParent(control.transform, false);
                            controlChild.transform.localPosition = child.transform.localPosition;
                            controlChild.transform.localRotation = child.transform.localRotation; controlChild.transform.localScale = child.transform.localScale;
                            var nativeShape = controlChild.AddComponent<CapsuleCollider>();
                            nativeShape.center = capsule.center; nativeShape.radius = capsule.radius;
                            nativeShape.height = capsule.height; nativeShape.direction = capsule.direction;
                            control.SetActive(true);
                            Physics.SyncTransforms();
                            Bounds actual = nativeShape.bounds;
                            CapsuleGeometry dimensions = nativeShape.GeometryHolder.As<CapsuleGeometry>();
                            control.SetActive(false);
                            target.transform.position = source.transform.position + actual.center + Vector3.up * (actual.extents.y + .5f);
                            bool updated = Query(out hit, out failure);
                            Debug.Log("BUILDWORKS_SURFACE_CAPSULE_AXIS_CONTROL bounds=" + actual + " radius=" + dimensions.Radius +
                                " halfLength=" + dimensions.HalfLength + " target=" + target.transform.position.ToString("F6") + " found=" + updated + " failure=" + failure);
                            Require(updated && hit.collider == support && Mathf.Abs(hit.point.y - (source.transform.position.y + actual.max.y)) < .002f,
                                "Cached capsule direction/dimensions differ from a fresh native collider");
                            Object.DestroyImmediate(control); owned.Remove(control);
                        }
                    }
                    Require(!source.activeSelf && child.GetComponents<Collider>().Length == 1,
                        "Adapter activated or altered the inactive gameplay source");
                    Debug.Log("BUILDWORKS_SURFACE_ADAPTER_CASE coordinate=" + coordinate + " kind=" + kind + " partner=" + targetKind +
                        " result=PASS cachedSceneCount=" + UnityEngine.SceneManagement.SceneManager.sceneCount);
                    adapter.Clear();
                    Object.DestroyImmediate(source); Object.DestroyImmediate(target); owned.Clear();
                }
            var emptyCompound = new GameObject("AdapterEmptyMeshCompound"); owned.Add(emptyCompound); emptyCompound.SetActive(false);
            emptyCompound.transform.position = new Vector3(340, 340, 340);
            var emptyParent = new GameObject("EmptyMeshParent"); emptyParent.transform.SetParent(emptyCompound.transform, false);
            var emptyMesh = emptyParent.AddComponent<MeshCollider>();
            var realChild = new GameObject("RealShape"); realChild.transform.SetParent(emptyParent.transform, false);
            realChild.transform.localPosition = Vector3.left * 3;
            var realBox = realChild.AddComponent<BoxCollider>();
            var emptyTarget = new GameObject("EmptyMeshWorldTarget") { layer = layer }; owned.Add(emptyTarget);
            var emptySupport = emptyTarget.AddComponent<BoxCollider>();
            emptyTarget.transform.position = realChild.transform.position + Vector3.up;
            RaycastHit emptyHit = default;
            bool EmptyQuery(out PlacementContactFailure failure)
            {
                bool found = adapter.TryFind(emptyCompound, 1 << layer,
                    emptyCompound.transform.position + Vector3.back * 5, 10, out emptyHit, out failure);
                Require(!found || emptyHit.collider == emptySupport, "Empty compound contacted an unexpected world collider");
                return found;
            }
            var receiverField = typeof(BlueprintSurfaceContact).GetField("receiver",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Require(EmptyQuery(out _), "An empty parent MeshCollider rejected the valid compound contact");
            var emptyPart = (BlueprintStructuralSnapshot.Part)receiverField.GetValue(adapter);
            Require(emptyPart.CompleteGeometry && emptyPart.Colliders.Count == 1 && !emptyPart.Copies.ContainsKey(emptyMesh) &&
                EmptyQuery(out _) && ReferenceEquals(emptyPart, receiverField.GetValue(adapter)),
                "Empty mesh was copied, made geometry incomplete or forced a cached rebuild");
            realChild.transform.localPosition = Vector3.left * 6;
            var transitionMesh = Object.Instantiate(cube); ownedMeshes.Add(transitionMesh);
            transitionMesh.name = "OwnedEmptyMeshTransition";
            emptyMesh.sharedMesh = transitionMesh; emptyMesh.convex = true;
            Require(emptyMesh.sharedMesh == transitionMesh, "The source did not acquire the owned transition mesh");
            emptyTarget.transform.position = emptyCompound.transform.position + Vector3.up;
            Require(EmptyQuery(out _), "Null-to-real mesh membership did not acquire its sole contact");
            emptyTarget.transform.position += Vector3.up * .01f;
            Require(!EmptyQuery(out _), "Empty-mesh transition bridged a1cm gap");
            emptyTarget.transform.position -= Vector3.up * .01f;
            var beforeNullPart = (BlueprintStructuralSnapshot.Part)receiverField.GetValue(adapter);
            Require(beforeNullPart.Copies.ContainsKey(emptyMesh), "The sole positive mesh was not in cached Copies before null");
            int beforeNullRoot = beforeNullPart.Root.GetInstanceID();
            // Destroy only the owned mesh: assigning null did not clear the built-in mesh in the saved engine run.
            Object.DestroyImmediate(transitionMesh);
            Mesh afterNullMesh = emptyMesh.sharedMesh;
            Debug.Log("BUILDWORKS_EMPTY_DESTROY_READBACK source=" + emptyMesh.GetInstanceID() + " mesh=" +
                (afterNullMesh ? afterNullMesh.name + ":" + afterNullMesh.GetInstanceID() : "Unity-null") +
                " managedNull=" + ReferenceEquals(afterNullMesh, null) + " empty=" + BlueprintStructuralSnapshot.IsEmptyMesh(emptyMesh) +
                " convex=" + emptyMesh.convex + " enabled=" + emptyMesh.enabled + " rootActive=" + emptyCompound.activeSelf);
            Require(BlueprintStructuralSnapshot.IsEmptyMesh(emptyMesh), "Destroying the owned mesh did not establish an empty source shape");
            bool afterNullFound = EmptyQuery(out var afterNullFailure);
            var afterNullPart = (BlueprintStructuralSnapshot.Part)receiverField.GetValue(adapter);
            Debug.Log("BUILDWORKS_EMPTY_NULL_RESULT found=" + afterNullFound + " failure=" + afterNullFailure +
                " samePart=" + ReferenceEquals(beforeNullPart, afterNullPart) + " oldRoot=" + beforeNullRoot +
                " oldActive=" + (beforeNullPart.Root && beforeNullPart.Root.activeInHierarchy) +
                " newRoot=" + afterNullPart.Root.GetInstanceID() + " complete=" + afterNullPart.CompleteGeometry +
                " reason=" + afterNullPart.UnknownReason + " emptyCopied=" + afterNullPart.Copies.ContainsKey(emptyMesh) +
                " sourceOrigin=" + emptyCompound.transform.position.ToString("F6") + " childPosition=" + realChild.transform.position.ToString("F6") +
                " childScale=" + realChild.transform.lossyScale.ToString("F6") + " boxCenter=" + realBox.center.ToString("F6") +
                " boxSize=" + realBox.size.ToString("F6") + " target=" + emptyTarget.transform.position.ToString("F6") +
                " targetBounds=" + emptySupport.bounds + " hit=" + (emptyHit.collider ? emptyHit.collider.GetInstanceID() : 0) +
                " hitPoint=" + emptyHit.point.ToString("F6"));
            foreach (var pair in afterNullPart.Copies)
                Debug.Log("BUILDWORKS_EMPTY_NULL_COPY source=" + pair.Key.GetInstanceID() + ":" + pair.Key.GetType().Name +
                    " copy=" + pair.Value.GetInstanceID() + ":" + pair.Value.GetType().Name + " active=" + pair.Value.gameObject.activeInHierarchy +
                    " bounds=" + pair.Value.bounds + " worldCenter=" + (pair.Value.bounds.center + emptyCompound.transform.position).ToString("F6"));
            Require(!afterNullFound, "A destroyed owned mesh retained a stale contact");
            Require(!ReferenceEquals(beforeNullPart, afterNullPart) && afterNullPart.Colliders.Count == 1 &&
                !afterNullPart.Copies.ContainsKey(emptyMesh), "Null mesh membership did not rebuild to the remaining real shape");
            emptyTarget.transform.position = realChild.transform.position + Vector3.up;
            Require(EmptyQuery(out _), "Removing a mesh lost the remaining real collider");
            emptyPart = (BlueprintStructuralSnapshot.Part)receiverField.GetValue(adapter);
            Require(emptyPart.CompleteGeometry && emptyPart.Colliders.Count == 1 && !emptyPart.Copies.ContainsKey(emptyMesh) &&
                EmptyQuery(out _) && ReferenceEquals(emptyPart, receiverField.GetValue(adapter)), "Null membership did not stabilize");
            realBox.enabled = false;
            Require(!EmptyQuery(out _), "A null-only compound granted attachment");
            realBox.enabled = true; realChild.transform.localScale = new Vector3(-1, 1, 1);
            Require(!EmptyQuery(out var invalidFailure) && (invalidFailure & PlacementContactFailure.UnknownGeometry) != 0,
                "Skipping an empty mesh concealed an invalid real shape");
            Require(!emptyCompound.activeSelf && !emptyMesh.sharedMesh, "Empty-mesh query changed the inactive source");
            Debug.Log("BUILDWORKS_SURFACE_EMPTY_MESH_OK parent/compound/cache/null-real-destroyed/gap/invalid; null-setter transition not proved");
            adapter.Clear(); Object.DestroyImmediate(emptyCompound); Object.DestroyImmediate(emptyTarget); owned.Clear();

            foreach (bool triangleTarget in new[] { false, true })
            {
            var hollowSource = new GameObject("AdapterHollowSource"); owned.Add(hollowSource); hollowSource.SetActive(false);
            hollowSource.transform.position = new Vector3(300, 300, 300);
            var frame = new Mesh { name = "OwnedHollowContactFrame" }; ownedMeshes.Add(frame);
            var combine = new CombineInstance[4];
            Vector3[] positions = { Vector3.left * 1.5f, Vector3.right * 1.5f, Vector3.down * 1.5f, Vector3.up * 1.5f };
            for (int i = 0; i < 4; ++i)
                combine[i] = new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(positions[i], Quaternion.identity,
                    i < 2 ? new Vector3(1, 4, 1) : new Vector3(2, 1, 1)) };
            frame.CombineMeshes(combine);
            var hollow = hollowSource.AddComponent<MeshCollider>(); hollow.sharedMesh = frame; hollow.convex = false;
            var interior = new GameObject("AdapterHollowInteriorTarget") { layer = layer }; owned.Add(interior);
            interior.transform.position = hollowSource.transform.position;
            Collider interiorCollider;
            if (triangleTarget) { var c = interior.AddComponent<MeshCollider>(); c.sharedMesh = cube; interiorCollider = c; }
            else interiorCollider = interior.AddComponent<BoxCollider>();
            PlacementContactFailure hollowFailure = default;
            bool HollowQuery(out RaycastHit hit) => adapter.TryFind(hollowSource, 1 << layer,
                interior.transform.position + Vector3.back * 5, 10, out hit, out hollowFailure);
            Require(!HollowQuery(out _), "A hollow mesh's bounds granted contact inside its empty hole");
            if (triangleTarget)
            {
                Vector3[] vertices = frame.vertices;
                int[] originalIndices = frame.triangles, filledIndices = (int[])frame.triangles.Clone();
                Vector3[] corners = { new Vector3(-2,-2,-.5f), new Vector3(2,-2,-.5f),
                    new Vector3(2,2,-.5f), new Vector3(-2,2,-.5f) };
                int[] panel = corners.Select(p => Array.FindIndex(vertices, v => v == p)).ToArray();
                Require(panel.All(i => i >= 0), "Owned frame lacks the independently specified panel corners");
                int[] order = { 0,1,2,0,2,3 };
                for (int i = 0; i < order.Length; ++i) filledIndices[i] = panel[order[i]];
                frame.SetIndices(filledIndices, MeshTopology.Triangles, 0, false);
                Require(HollowQuery(out var panelHit) && panelHit.collider == interiorCollider,
                    "TriTri same-ID/count/bounds index edit did not acquire the real panel contact");
                frame.SetIndices(originalIndices, MeshTopology.Triangles, 0, false);
                Require(!HollowQuery(out _), "TriTri cached the removed panel across validations");
                Debug.Log("BUILDWORKS_TRITRI_INDEX_REFRESH_OK empty/panel/empty with unchanged mesh ID/count/bounds");
            }
            hollow.convex = true;
            bool filledFound = HollowQuery(out var filledHit);
            var observedPart = receiverField.GetValue(adapter) as BlueprintStructuralSnapshot.Part;
            var observedCopy = observedPart?.Colliders.OfType<MeshCollider>().FirstOrDefault();
            Debug.Log("BUILDWORKS_HOLLOW_FILLED_RESULT triangleTarget=" + triangleTarget + " found=" + filledFound +
                " failure=" + hollowFailure + " hit=" + (filledHit.collider ? filledHit.collider.GetInstanceID() : 0) +
                " expected=" + interiorCollider.GetInstanceID() + " sourceConvex=" + hollow.convex +
                " copy=" + (observedCopy ? observedCopy.GetInstanceID() : 0) + " copyConvex=" + (observedCopy && observedCopy.convex));
            if (triangleTarget && observedCopy && observedCopy.convex && observedCopy.gameObject.activeInHierarchy)
            {
                bool overlap = Physics.ComputePenetration(observedCopy, observedCopy.transform.position, observedCopy.transform.rotation,
                    interiorCollider, interiorCollider.transform.position - hollowSource.transform.position, interiorCollider.transform.rotation,
                    out Vector3 direction, out float distance);
                Debug.Log("BUILDWORKS_HOLLOW_OVERLAP_ORACLE result=" + overlap +
                    (overlap ? " direction=" + direction + " distance=" + distance.ToString("R") : "; outputs undefined"));
            }
            else if (triangleTarget) Debug.Log("BUILDWORKS_HOLLOW_OVERLAP_ORACLE skipped: copied convex shape not established");
            Require(filledFound && filledHit.collider == interiorCollider,
                "Convex filled-hull positive control or cached convex refresh failed");
            interior.transform.position += Vector3.forward * 1.01f;
            Require(!HollowQuery(out _), "Contained-target fallback accepted a1cm air gap");
            interior.transform.position -= Vector3.forward * 1.01f;
            Vector3[] movedVertices = frame.vertices;
            for (int i = 0; i < movedVertices.Length; ++i) movedVertices[i] += Vector3.up * 10;
            frame.vertices = movedVertices; frame.RecalculateBounds();
            Require(!HollowQuery(out _), "Cached mesh retained its old collision after in-place vertices changed");
            interior.transform.position += Vector3.up * 10;
            Require(HollowQuery(out filledHit) && filledHit.collider == interiorCollider,
                "In-place mesh recook did not acquire its new contact");
            Debug.Log("BUILDWORKS_SURFACE_ADAPTER_HOLLOW_OK empty-hole/filled-hull/inplace-mesh refresh; triangleTarget=" + triangleTarget);
            adapter.Clear();
            foreach (GameObject item in owned) if (item) Object.DestroyImmediate(item);
            owned.Clear();
            }
        }
        catch (Exception caught) { error = caught; }
        finally
        {
            adapter.Dispose();
            foreach (GameObject item in owned) if (item) Object.DestroyImmediate(item);
        }
        while (adapter.Unloading != null && !adapter.Unloading.isDone) yield return null;
        foreach (Mesh mesh in ownedMeshes) if (mesh) Object.DestroyImmediate(mesh);
        Require(UnityEngine.SceneManagement.SceneManager.sceneCount == sceneCount, "Adapter leaked its private physics scene");
        if (error != null) throw error;
        Debug.Log("BUILDWORKS_SURFACE_ADAPTER_OK actual adapter/witness/cache fixture; not game75/native wear proof");
    }

    private static IEnumerable ObserveInactiveShapeQueries()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("BuildWorksShapeApi",
            new UnityEngine.SceneManagement.CreateSceneParameters(UnityEngine.SceneManagement.LocalPhysicsMode.Physics3D));
        var owned = new List<GameObject>();
        Exception error = null;
        AsyncOperation unloading = null;
        try
        {
            var meshSource = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(meshSource);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(meshSource, scene);
            Mesh cube = meshSource.GetComponent<MeshFilter>().sharedMesh;
            meshSource.SetActive(false);
            var meshPartner = new GameObject("ImmediateMeshPartner"); owned.Add(meshPartner);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(meshPartner, scene);
            meshPartner.transform.position = new Vector3(300, 30, 300);
            var meshPartnerCollider = meshPartner.AddComponent<MeshCollider>(); meshPartnerCollider.sharedMesh = cube;
            Vector3 virtualPosition = new Vector3(17, 23, -11);
            Quaternion rotation = Quaternion.Euler(0, 0, 90);
            foreach (string kind in new[] { "Box", "Sphere", "Capsule", "ConvexMesh", "NonconvexMesh" })
            {
                var item = new GameObject("ShapeApi_" + kind); owned.Add(item); item.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item, scene);
                Vector3 physicalPosition = kind == "NonconvexMesh" ? new Vector3(101, 30, 101) : new Vector3(12000, 30, 12000);
                item.transform.SetPositionAndRotation(physicalPosition, rotation);
                item.transform.localScale = new Vector3(2, 1, .5f);
                Collider collider;
                if (kind == "Box") collider = item.AddComponent<BoxCollider>();
                else if (kind == "Sphere") collider = item.AddComponent<SphereCollider>();
                else if (kind == "Capsule")
                {
                    var capsule = item.AddComponent<CapsuleCollider>(); capsule.radius = .5f;
                    capsule.height = 4; capsule.direction = 1; collider = capsule;
                }
                else
                {
                    var mesh = item.AddComponent<MeshCollider>(); mesh.sharedMesh = cube;
                    mesh.convex = kind == "ConvexMesh"; collider = mesh;
                }
                foreach (string state in new[] { "coldInactive", "active", "warmInactive", "disabled" })
                {
                    item.SetActive(state == "active" || state == "disabled"); collider.enabled = state != "disabled";
                    Physics.SyncTransforms();
                    string geometry;
                    try
                    {
                        GeometryHolder holder = collider.GeometryHolder;
                        geometry = holder.Type.ToString();
                        if (kind == "Box") geometry += " halfExtents=" + holder.As<BoxGeometry>().HalfExtents.ToString("F4");
                        else if (kind == "Sphere") geometry += " radius=" + holder.As<SphereGeometry>().Radius;
                        else if (kind == "Capsule") geometry += " radius=" + holder.As<CapsuleGeometry>().Radius +
                            " halfLength=" + holder.As<CapsuleGeometry>().HalfLength;
                        else if (kind == "ConvexMesh") geometry += " scale=" + holder.As<ConvexMeshGeometry>().Scale.ToString("F4");
                        else geometry += " scale=" + holder.As<TriangleMeshGeometry>().Scale.ToString("F4");
                    }
                    catch (Exception caught) { geometry = caught.GetType().Name + ": " + caught.Message; }
                    Debug.Log("BUILDWORKS_SHAPE_GEOMETRY_OBSERVATION shape=" + kind + " state=" + state + " geometry=" + geometry);
                    bool matched = false;
                    bool? rayFound = null, rayIdentity = null;
                    string observedError = null;
                    Vector3 actual = default;
                    Vector3 expected = kind == "Capsule" ? virtualPosition + Vector3.right * 2 : virtualPosition + Vector3.up;
                    try
                    {
                        if (kind == "NonconvexMesh")
                        {
                            expected = item.transform.position + Vector3.forward * .25f;
                            bool found = collider.Raycast(new Ray(item.transform.position + Vector3.forward * 5, Vector3.back),
                                out RaycastHit hit, 10);
                            rayFound = found; rayIdentity = hit.collider == collider;
                            actual = hit.point;
                            matched = found && hit.collider == collider && Vector3.Distance(actual, expected) < .001f;
                        }
                        else
                        {
                            Vector3 point = virtualPosition + (kind == "Capsule" ? Vector3.right : Vector3.up) * 5;
                            actual = Physics.ClosestPoint(point, collider, virtualPosition, rotation);
                            matched = Vector3.Distance(actual, expected) < .001f;
                        }
                    }
                    catch (Exception caught) { observedError = caught.GetType().Name + ": " + caught.Message; }
                    Debug.Log("BUILDWORKS_SHAPE_API_OBSERVATION shape=" + kind + " state=" + state +
                        " matched=" + matched + " actual=" + actual.ToString("F6") + " expected=" + expected.ToString("F6") +
                        " rayFound=" + rayFound + " rayIdentity=" + rayIdentity +
                        " error=" + (observedError ?? "none"));
                    if (state == "active") Require(matched, "Active shape API baseline failed: " + kind + ": " + observedError);
                    if (state == "active") ObserveImmediateContacts(collider, kind, rotation);
                    if (state == "active") ObserveImmediateContacts(collider, kind, rotation, meshPartnerCollider);
                    Require(item.transform.position == physicalPosition && item.transform.rotation == rotation &&
                        item.transform.localScale == new Vector3(2, 1, .5f), "Shape API query changed source pose");
                }
            }
        }
        catch (Exception caught) { error = caught; }
        finally
        {
            foreach (GameObject item in owned) if (item) Object.DestroyImmediate(item);
            if (scene.IsValid()) unloading = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }
        while (unloading != null && !unloading.isDone) yield return null;
        Require(!scene.IsValid(), "Shape API observation scene leaked");
        if (error != null) throw new InvalidOperationException("Shape API observations failed", error);
        Debug.Log("BUILDWORKS_SHAPE_API_OBSERVATION_COMPLETE inspect individual inactive results before adoption");
    }

    private static void ObserveImmediateContacts(Collider collider, string kind, Quaternion rotation, Collider meshPartner = null)
    {
        NativeArray<GeometryHolder> a = default, b = default;
        NativeArray<ImmediateTransform> pa = default, pb = default;
        NativeArray<ImmediateContact> contacts = default;
        NativeArray<int> counts = default;
        try
        {
            a = new NativeArray<GeometryHolder>(1, Allocator.Temp); b = new NativeArray<GeometryHolder>(1, Allocator.Temp);
            pa = new NativeArray<ImmediateTransform>(1, Allocator.Temp); pb = new NativeArray<ImmediateTransform>(1, Allocator.Temp);
            contacts = new NativeArray<ImmediateContact>(64, Allocator.Temp); counts = new NativeArray<int>(1, Allocator.Temp);
            GeometryHolder source = collider.GeometryHolder;
            GeometryHolder box = meshPartner ? meshPartner.GeometryHolder : GeometryHolder.Create(new BoxGeometry(Vector3.one * .5f));
            string partner = meshPartner ? "TriangleMesh" : "Box";
            Quaternion pose = kind == "Capsule" ? rotation * Quaternion.FromToRotation(Vector3.right, Vector3.up) : rotation;
            foreach (bool reverse in new[] { false, true })
                foreach (float gap in new[] { 0f, .01f })
                {
                    var sourcePose = new ImmediateTransform { Position = Vector3.zero, Rotation = pose };
                    var boxPose = new ImmediateTransform { Position = Vector3.up * (1.5f + gap), Rotation = Quaternion.identity };
                    a[0] = reverse ? box : source; b[0] = reverse ? source : box;
                    pa[0] = reverse ? boxPose : sourcePose; pb[0] = reverse ? sourcePose : boxPose; counts[0] = 0;
                    int total;
                    try
                    {
                        total = ImmediatePhysics.GenerateContacts(a.AsReadOnly(), b.AsReadOnly(), pa.AsReadOnly(), pb.AsReadOnly(),
                            1, contacts, counts, .002f);
                    }
                    catch (Exception caught)
                    {
                        Debug.Log("BUILDWORKS_IMMEDIATE_CONTACT_OBSERVATION shape=" + kind + " partner=" + partner + " reverse=" + reverse +
                            " gap=" + gap + " error=" + caught.GetType().Name + ": " + caught.Message);
                        if (kind != "NonconvexMesh") throw;
                        continue;
                    }
                    Require(total >= 0 && total < contacts.Length && counts[0] == total,
                        "Immediate contact result incomplete or inconsistent: " + kind);
                    for (int i = 0; i < total; ++i)
                    {
                        var value = contacts[i];
                        Require(!float.IsNaN(value.Separation) && !float.IsInfinity(value.Separation) &&
                            Mathf.Abs(value.Separation) <= .002f &&
                            !float.IsNaN(value.Point.sqrMagnitude) && !float.IsInfinity(value.Point.sqrMagnitude) &&
                            Mathf.Abs(value.Point.y - 1) < .002f && Mathf.Abs(value.Normal.y) > .99f,
                            "Immediate contact has an invalid witness or pose: " + kind);
                    }
                    Debug.Log("BUILDWORKS_IMMEDIATE_CONTACT_OBSERVATION shape=" + kind + " partner=" + partner + " reverse=" + reverse +
                        " gap=" + gap + " count=" + total + (total == 0 ? "" : " separation=" + contacts[0].Separation +
                        " point=" + contacts[0].Point.ToString("F6") + " normal=" + contacts[0].Normal.ToString("F6")));
                    if (gap > 0) Require(total == 0, "Immediate contacts bridged a clear gap: " + kind);
                    else if (kind != "NonconvexMesh") Require(total > 0, "Immediate baseline contact missed: " + kind);
                }
        }
        finally
        {
            if (a.IsCreated) a.Dispose(); if (b.IsCreated) b.Dispose();
            if (pa.IsCreated) pa.Dispose(); if (pb.IsCreated) pb.Dispose();
            if (contacts.IsCreated) contacts.Dispose(); if (counts.IsCreated) counts.Dispose();
        }
    }

    // Geometry-only proxy route: no native WearNTear flag, placement payment or world anchor proof.
    private static IEnumerable VerifyFinalPoseContact()
    {
        const int layer = 8;
        Vector3 position = new Vector3(16000, 20, 16000);
        Vector3 center = position + new Vector3(-.5f, .5f, 0);
        var source = new GameObject("ContactInactiveSource"); source.SetActive(false);
        var child = new GameObject("OffsetContactBox"); child.transform.SetParent(source.transform, false);
        child.transform.localPosition = new Vector3(.25f, .5f, 0);
        child.AddComponent<BoxCollider>();
        var owned = new List<GameObject>();
        var snapshot = new BlueprintStructuralSnapshot();
        Exception error = null;
        try
        {
            var final = snapshot.Add("contactFinal", source, position, Quaternion.Euler(0, 0, 90), new Vector3(2, 1, .5f));
            var shape = final.Colliders.Single();
            Physics.SyncTransforms();
            Require(final.Root.activeInHierarchy && final.Root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 &&
                final.Root.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Contact proxy contains gameplay components");
            Require(Vector3.Distance(shape.bounds.center, center) < .002f &&
                Vector3.Distance(shape.bounds.extents, new Vector3(.5f, 1, .25f)) < .002f,
                "Final contact geometry ignored rotation, scale or child offset");
            // Scalar Workbench stand-in selects the production wear branch; no native wear behaviour runs here.
            final.Root.AddComponent<WearNTear>();

            BoxCollider Anchor(string name, Vector3 offset, Vector3 size)
            {
                var item = new GameObject(name) { layer = layer }; owned.Add(item);
                item.transform.position = center + offset;
                var collider = item.AddComponent<BoxCollider>(); collider.size = size;
                Physics.SyncTransforms(); return collider;
            }
            bool Query(BlueprintStructuralSnapshot.Part part, out RaycastHit hit, out PlacementContactFailure failure,
                bool surfaceAttachment = true)
            {
                Bounds bounds = part.Colliders[0].bounds;
                foreach (Collider collider in part.Colliders) bounds.Encapsulate(collider.bounds);
                return BuildWorksPlacementValidation.TryFindContact(part.Root, bounds,
                    1 << layer, center + Vector3.back * 5, 10, PlacementContactSides.All, out hit, out failure,
                    surfaceAttachment: surfaceAttachment);
            }
            void RemoveAnchors()
            {
                foreach (GameObject item in owned) Object.DestroyImmediate(item);
                owned.Clear(); Physics.SyncTransforms();
            }

            var side = Anchor("SideContact", Vector3.right, Vector3.one);
            Require(Query(final, out var hit, out var failure) && failure == PlacementContactFailure.None &&
                hit.collider == side && Mathf.Abs(hit.point.x - (center.x + .5f)) < .002f &&
                Mathf.Abs(hit.point.y - center.y) <= .501f && Mathf.Abs(hit.point.z - center.z) <= .251f,
                "Side contact lacks a real shared-face witness");
            RemoveAnchors();
            var top = Anchor("CeilingContact", Vector3.up * 1.5f, Vector3.one);
            Require(Query(final, out hit, out failure) && failure == PlacementContactFailure.None && hit.collider == top &&
                Mathf.Abs(hit.point.y - (center.y + 1)) < .002f &&
                Mathf.Abs(hit.point.x - center.x) <= .501f && Mathf.Abs(hit.point.z - center.z) <= .251f,
                "Ceiling contact lacks a real shared-face witness");
            var originalPose = snapshot.Add("contactOriginalPose", source, position, Quaternion.identity, Vector3.one);
            Physics.SyncTransforms();
            Require(!Query(originalPose, out _, out failure) && failure == PlacementContactFailure.NoContact,
                "Original geometry incorrectly reaches the final-pose ceiling");
            Require(Query(final, out hit, out failure) && hit.collider == top, "Final-pose control disturbed contact");
            RemoveAnchors();
            Anchor("ContactGap", Vector3.right * 1.125f, Vector3.one);
            Require(!Query(final, out _, out failure) && failure == PlacementContactFailure.NoContact,
                "A .125m gap was accepted as physical contact");
            RemoveAnchors();
            Anchor("NativeEnvelopeGap", Vector3.right * 1.07f, Vector3.one);
            Require(!Query(final, out _, out failure) && failure == PlacementContactFailure.NoContact,
                "WearNTear .15m envelope leaked into functional contact");
            Require(Query(final, out _, out _, surfaceAttachment: false),
                "Strict functional mode changed the legacy native support envelope");
            RemoveAnchors();
            Anchor("HollowLeft", Vector3.left * 2, new Vector3(.25f, 6, 6));
            Anchor("HollowRight", Vector3.right * 2, new Vector3(.25f, 6, 6));
            Anchor("HollowBottom", Vector3.down * 3, new Vector3(4, .25f, 6));
            Anchor("HollowTop", Vector3.up * 3, new Vector3(4, .25f, 6));
            Bounds hollowBounds = owned[0].GetComponent<Collider>().bounds;
            foreach (GameObject item in owned) hollowBounds.Encapsulate(item.GetComponent<Collider>().bounds);
            Require(hollowBounds.Contains(center) && hollowBounds.Intersects(shape.bounds), "Hollow AABB control is invalid");
            Require(!Query(final, out _, out failure) && failure == PlacementContactFailure.NoContact,
                "Empty space inside compound bounds became contact");
            RemoveAnchors();
            var hollowSource = new GameObject("HollowContactSource"); hollowSource.SetActive(false); owned.Add(hollowSource);
            void Wall(Vector3 offset, Vector3 size)
            {
                var wall = new GameObject("HollowWall"); wall.transform.SetParent(hollowSource.transform, false);
                wall.transform.localPosition = offset; wall.AddComponent<BoxCollider>().size = size;
            }
            Wall(Vector3.left * 2, new Vector3(.25f, 6, 6));
            Wall(Vector3.right * 2, new Vector3(.25f, 6, 6));
            Wall(Vector3.down * 3, new Vector3(4, .25f, 6));
            Wall(Vector3.up * 3, new Vector3(4, .25f, 6));
            var hollow = snapshot.Add("hollowReceiver", hollowSource, center, Quaternion.identity, Vector3.one);
            var endAnchor = Anchor("EmptyEndContact", Vector3.forward * 3.5f, Vector3.one);
            Bounds receiverBounds = hollow.Colliders[0].bounds;
            foreach (Collider collider in hollow.Colliders) receiverBounds.Encapsulate(collider.bounds);
            Require(receiverBounds.Contains(center) &&
                Mathf.Abs(receiverBounds.max.z - endAnchor.bounds.min.z) < .002f,
                "Hollow receiver does not meet the anchor's AABB end plane");
            Require(!Query(hollow, out _, out failure) && failure == PlacementContactFailure.NoContact,
                "Hollow receiver was replaced with its filled bounds");
            var filledSource = new GameObject("FilledBoundsControl"); filledSource.SetActive(false); owned.Add(filledSource);
            filledSource.AddComponent<BoxCollider>().size = receiverBounds.size;
            var filled = snapshot.Add("filledReceiver", filledSource, receiverBounds.center, Quaternion.identity, Vector3.one);
            Physics.SyncTransforms();
            Require(Query(filled, out hit, out failure) && failure == PlacementContactFailure.None && hit.collider == endAnchor,
                "Filled-bounds positive control did not distinguish the hollow receiver");
            Require(!source.activeSelf && source.transform.position == Vector3.zero &&
                source.transform.rotation == Quaternion.identity && source.transform.localScale == Vector3.one &&
                child.transform.localPosition == new Vector3(.25f, .5f, 0), "Contact query changed its original source");
        }
        catch (Exception caught) { error = caught; }
        finally
        {
            snapshot.Dispose();
            foreach (GameObject item in owned) if (item) Object.DestroyImmediate(item);
            Object.DestroyImmediate(source);
        }
        while (snapshot.Unloading != null && !snapshot.Unloading.isDone) yield return null;
        Require(!snapshot.IsSceneLoaded, "Contact proxy scene leaked after cleanup");
        if (error != null) throw new InvalidOperationException("Final-pose contact fixture failed", error);
        Debug.Log("BUILDWORKS_FINAL_POSE_CONTACT_OK proxy_geometry_only; no native wear or world-anchor proof");
    }

    private static void VerifyContactMeshReads()
    {
        var owned = new List<Mesh>();
        Mesh Make(Vector3[] points)
        {
            var result = new Mesh { name = "OwnedContactReadFixture", vertices = points };
            owned.Add(result); return result;
        }
        try
        {
            Vector3[] points = { Vector3.zero, Vector3.right, Vector3.up,
                Vector3.forward, Vector3.forward + Vector3.right, Vector3.forward + Vector3.up };
            Mesh mesh = Make(points); mesh.subMeshCount = 2;
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false, 3);
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 1, false);
            int vertices = 0, triangles = 0;
            Require(BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles,
                out Vector3[] firstPoints, out int[] firstIndices, out string error), error);
            Require(vertices == 6 && triangles == 2 && firstPoints.SequenceEqual(points) &&
                firstIndices.SequenceEqual(new[] { 3, 4, 5, 0, 1, 2 }), "Contact read lost a submesh or baseVertex");
            Bounds originalBounds = mesh.bounds;
            points[1] = Vector3.right * .5f; mesh.vertices = points; mesh.bounds = originalBounds;
            mesh.SetIndices(new[] { 2, 1, 0 }, MeshTopology.Triangles, 0, false, 3);
            Require(BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles,
                out Vector3[] changedPoints, out int[] changedIndices, out error), error);
            Require(changedPoints[1] == points[1] && changedIndices[0] == 5 && firstPoints[1] == Vector3.right &&
                firstIndices[0] == 3 && !ReferenceEquals(firstPoints, changedPoints), "Contact read retained stale mesh data");
            vertices = TriangleContactCandidates.MaximumVerticesPerValidation - 6;
            triangles = TriangleContactCandidates.MaximumTrianglesPerValidation - 2;
            Require(BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles,
                out _, out _, out error), error);
            Require(vertices == TriangleContactCandidates.MaximumVerticesPerValidation &&
                triangles == TriangleContactCandidates.MaximumTrianglesPerValidation, "Contact read changed shared budget accounting");
            Require(!BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles,
                out changedPoints, out changedIndices, out _) && changedPoints.Length == 0 && changedIndices.Length == 0,
                "Exhausted read budget returned partial or stale data");
            vertices = -1; triangles = 0;
            Require(!BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles, out _, out _, out _),
                "Contact reader accepted an invalid shared budget");
            vertices = triangles = 0;
            mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 1, false);
            Require(!BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles,
                out changedPoints, out changedIndices, out _) && vertices == 0 && triangles == 0 &&
                changedPoints.Length == 0 && changedIndices.Length == 0, "Contact reader silently skipped a nontriangle submesh");
            mesh.SetIndices(Array.Empty<int>(), MeshTopology.Triangles, 0, false);
            mesh.SetIndices(Array.Empty<int>(), MeshTopology.Triangles, 1, false);
            Require(!BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles, out _, out _, out _),
                "Empty raw triangles were reported as a usable contact mesh");
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
            mesh.UploadMeshData(true);
            Require(!mesh.isReadable && !BlueprintEditorMeshData.TryReadContact(mesh, ref vertices, ref triangles,
                out _, out _, out _) && vertices == 0 && triangles == 0, "Contact reader fell back from an unreadable mesh");
            Mesh oversized = Make(new Vector3[TriangleContactCandidates.MaximumVerticesPerMesh + 1]);
            oversized.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
            Require(!BlueprintEditorMeshData.TryReadContact(oversized, ref vertices, ref triangles, out _, out _, out _) &&
                vertices == 0 && triangles == 0, "Contact reader allocated past its vertex cap");
            Mesh excessiveParts = Make(new[] { Vector3.zero, Vector3.right, Vector3.up }); excessiveParts.subMeshCount = 257;
            Require(!BlueprintEditorMeshData.TryReadContact(excessiveParts, ref vertices, ref triangles, out _, out _, out _),
                "Contact reader accepted unbounded submesh work");
            Mesh max = Make(new Vector3[TriangleContactCandidates.MaximumVerticesPerMesh]);
            int[] full = new int[TriangleContactCandidates.MaximumTrianglesPerMesh * 3];
            for (int i = 0; i < full.Length; ++i) full[i] = i % 3;
            max.SetIndices(full, MeshTopology.Triangles, 0, false);
            Require(BlueprintEditorMeshData.TryReadContact(max, ref vertices, ref triangles, out _, out _, out error) &&
                vertices == TriangleContactCandidates.MaximumVerticesPerMesh &&
                triangles == TriangleContactCandidates.MaximumTrianglesPerMesh, error ?? "Exact read caps failed");
            Mesh tooManyTriangles = Make(new[] { Vector3.zero, Vector3.right, Vector3.up });
            tooManyTriangles.SetIndices(new int[(TriangleContactCandidates.MaximumTrianglesPerMesh + 1) * 3], MeshTopology.Triangles, 0, false);
            vertices = triangles = 0;
            Require(!BlueprintEditorMeshData.TryReadContact(tooManyTriangles, ref vertices, ref triangles, out _, out _, out _) &&
                vertices == 0 && triangles == 0, "Contact reader allocated past its triangle cap");
            Mesh invalid = Make(new[] { Vector3.zero, new Vector3(float.NaN, 0, 0), Vector3.up });
            invalid.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
            Require(!BlueprintEditorMeshData.TryReadContact(invalid, ref vertices, ref triangles,
                out changedPoints, out changedIndices, out _) && vertices == 3 && triangles == 1 &&
                changedPoints.Length == 0 && changedIndices.Length == 0, "Failed finite validation reset budget or exposed partial geometry");
            Debug.Log("BUILDWORKS_CONTACT_MESH_READ_OK submeshes/baseVertex/freshness/CPU-only/shared-budgets/finite/limits; raw geometry only");
        }
        finally { foreach (Mesh mesh in owned) if (mesh) Object.DestroyImmediate(mesh); }
    }

    private static void VerifyNativeTriangleWitnessPrecision()
    {
        var meshes = new List<Mesh>();
        var source = new GameObject("NativeTrianglePrecisionSource");
        var target = new GameObject("NativeTrianglePrecisionWorldTarget");
        var query = new BlueprintTriangleContact();
        Mesh Plane(float x)
        {
            var mesh = new Mesh { name = "OwnedPrecisionPlane" };
            meshes.Add(mesh);
            mesh.vertices = new[] { new Vector3(x,-1,-1), new Vector3(x,1,-1), new Vector3(x,1,1), new Vector3(x,-1,1) };
            mesh.triangles = new[] { 0,1,2,0,2,3 };
            return mesh;
        }
        void VerifyPlane(Ray ray, Vector3 anchor, float x, Point3 witnessed)
        {
            Point3 start = new Point3((double)ray.origin.x - anchor.x, (double)ray.origin.y - anchor.y, (double)ray.origin.z - anchor.z);
            Point3 direction = Point(ray.direction);
            double distance = (x - start.X) / direction.X;
            Point3 analytic = start + direction * distance;
            Require(distance >= 0 && (analytic - witnessed).LengthSquared < 1e-14,
                "Native scalar ray distance disagrees with the independent plane intersection");
        }
        try
        {
            const float firstX = .001f;
            float tolerance = BuildWorksPlacementValidation.SurfaceContactTolerance;
            var a = source.AddComponent<MeshCollider>(); a.sharedMesh = Plane(firstX);
            var b = target.AddComponent<MeshCollider>();
            int cases = 0, roundedFalsePositive = 0;
            foreach (float coordinate in new[] { 0f, 101f, 12000f, -12000f })
              foreach (Vector3 direction in new[] { Vector3.right, new Vector3(1,.4f,.3f).normalized })
                foreach (float secondX in new[] { .0029f, .003f, .00305f, .0031f, .011f })
                {
                    target.transform.position = Vector3.one * coordinate;
                    b.sharedMesh = Plane(secondX);
                    Physics.SyncTransforms(); query.Clear();
                    Require(query.TryWitness(a, new Point3(firstX,0,0), direction, Vector3.zero,
                        out RaycastHit firstHit, out Point3 first, out Ray firstRay), "Native source plane witness missing");
                    Require(query.TryWitness(b, new Point3(secondX,0,0), direction, target.transform.position,
                        out RaycastHit secondHit, out Point3 second, out Ray secondRay), "Native world plane witness missing");
                    VerifyPlane(firstRay, Vector3.zero, firstX, first);
                    VerifyPlane(secondRay, target.transform.position, secondX, second);
                    double truth = (double)secondX - firstX, measured = second.X - first.X;
                    Require(BlueprintTriangleContact.TryRepresentationUpperBound(firstRay, firstHit.distance, first,
                        secondRay, secondHit.distance, second, out double upper), "Native witness representation bounds missing");
                    if (direction == Vector3.right && truth < tolerance - 1e-6)
                        Require(upper <= tolerance, "Conservative guard lost a clearly below-threshold pair");
                    if (truth > tolerance + 1e-6) Require(upper > tolerance, "Conservative guard accepted an above-threshold pair");
                    double rounded = (double)secondHit.point.x - target.transform.position.x - firstHit.point.x;
                    if (truth > tolerance && rounded <= tolerance) ++roundedFalsePositive;
                    Require(Math.Abs(truth - measured) < 1e-7, "Native ray witness lost sub-world-ULP precision");
                    if (Math.Abs(truth - tolerance) > 1e-6)
                        Require((measured <= tolerance) == (truth <= tolerance), "Native witness misclassified the2mm boundary");
                    if (truth > tolerance + 1e-6)
                        Require((second - first).LengthSquared > (double)tolerance * tolerance,
                            "Paired native witnesses accepted a real gap above tolerance");
                    Debug.Log("BUILDWORKS_TRIANGLE_PRECISION_CASE coordinate=" + coordinate + " direction=" + direction.ToString("F6") +
                        " truth=" + truth.ToString("R") + " measured=" + measured.ToString("R") + " rounded=" + rounded.ToString("R") +
                        " representationUpper=" + upper.ToString("R") + " nativeDistance=" + secondHit.distance.ToString("R") + " rayOrigin=" + secondRay.origin.ToString("F9") +
                        " rayDirection=" + secondRay.direction.ToString("F9"));
                    ++cases;
                }
            Require(cases == 40, "Native triangle precision fixture did not cover all cases");
            var roundingA = new Ray(new Vector3(-.0070000034756958485f,0,0), Vector3.right);
            var roundingB = new Ray(new Vector3(-.0050000036135315895f,0,0), Vector3.right);
            const float roundedDistance = .00800000037997961f;
            Point3 reconstructedA = BlueprintTriangleContact.ReconstructRayPoint(roundingA, roundedDistance, Vector3.zero),
                reconstructedB = BlueprintTriangleContact.ReconstructRayPoint(roundingB, roundedDistance, Vector3.zero);
            Require((reconstructedB - reconstructedA).LengthSquared < (double)tolerance * tolerance &&
                BlueprintTriangleContact.TryRepresentationUpperBound(roundingA, roundedDistance, reconstructedA,
                    roundingB, roundedDistance, reconstructedB, out double roundingUpper) && roundingUpper > tolerance,
                "Scalar rounding counterexample was not classified Unknown");
            Debug.Log("BUILDWORKS_TRIANGLE_REPRESENTATION_GUARD_OK rounding-only counterexample Unknown; native solver envelope separate");
            Debug.Log("BUILDWORKS_TRIANGLE_PRECISION_OK cases=" + cases + " observedRoundedFalsePositive=" + roundedFalsePositive +
                "; near-exact2mm classification is not certified by the1e-7 plane error bound");
        }
        finally
        {
            query.Clear(); Object.DestroyImmediate(source); Object.DestroyImmediate(target);
            foreach (Mesh mesh in meshes) if (mesh) Object.DestroyImmediate(mesh);
        }
    }

    private static Point3 Point(Vector3 value) => new Point3(value.x, value.y, value.z);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Structural snapshot: " + message); }
}

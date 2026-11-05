using System;
using System.Collections;
using System.Collections.Generic;
using OstrixMods.BuildWorks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class PlacementContactAcceptance
{
    public static void Verify() => VerifyContacts(includeStorage: true);

    public static void VerifyContactOnly() => VerifyContacts(includeStorage: false);

    // Separate regression entry: the frozen 327 acceptance receipt predates these cases.
    public static void VerifyContactRegressions()
    {
        var owned = new List<GameObject>();
        try
        {
            Vector3 origin = new Vector3(4000, 0, 4000);
            GameObject Cube(string name, Vector3 position, Vector3 size)
            {
                var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                owned.Add(item); item.name = name; item.layer = 0;
                item.transform.position = position; item.transform.localScale = size;
                return item;
            }
            var ghost = Cube("RegressionGhost", origin + Vector3.up * .5f, Vector3.one);
            ghost.AddComponent<WearNTear>();
            var failures = new List<string>();
            void Expect(bool valid, string message) { if (!valid) failures.Add(message); }
            Vector3 originalPosition = ghost.transform.position;
            Quaternion originalRotation = ghost.transform.rotation;
            Vector3 originalScale = ghost.transform.localScale;
            var support = Cube("NarrowSupport", origin + new Vector3(.2f, -.5f, .2f),
                new Vector3(.04f, 1, .04f));
            Physics.SyncTransforms();
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Regression bounds");
            Vector3 eye = origin + new Vector3(0, 3, -4);
            Require(Mathf.Abs(support.GetComponent<Collider>().bounds.max.y - bounds.min.y) < .0001f,
                "Narrow support fixture has a gap");
            Expect(BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 50,
                PlacementContactSides.Below, out RaycastHit hit) && hit.collider.gameObject == support,
                "Real narrow support between probe samples was missed");
            Expect(hit.collider && Mathf.Abs(hit.point.y - origin.y) < .0001f &&
                Mathf.Abs(hit.point.x - origin.x - .2f) <= .0201f &&
                Mathf.Abs(hit.point.z - origin.z - .2f) <= .0201f &&
                Vector3.Dot(hit.normal, Vector3.up) > .999f,
                "Narrow support supplied an invented point/normal");
            support.transform.position = origin + Vector3.up * .5f;
            support.transform.localScale = new Vector3(.1f, 2, 2);
            Physics.SyncTransforms();
            Require(support.GetComponent<Collider>().bounds.Contains(bounds.center), "Inside-origin fixture");
            Expect(BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 50,
                PlacementContactSides.All, out hit) && hit.collider.gameObject == support,
                "Intersecting structural support containing probe origins was missed");
            float localX = hit.point.x - origin.x;
            Expect(hit.collider && Mathf.Abs(Mathf.Abs(localX) - .05f) < .0003f &&
                Mathf.Abs(hit.point.z - origin.z) <= .5003f &&
                hit.point.y >= origin.y - .0003f && hit.point.y <= origin.y + 1.0003f &&
                hit.normal.x * Mathf.Sign(localX) > .999f,
                "Inside-origin support supplied an invented boundary point/normal");
            support.SetActive(false); Physics.SyncTransforms();
            Expect(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 50,
                PlacementContactSides.All, out _), "Regression solver invented absent support");
            Expect(ghost.transform.position == originalPosition && ghost.transform.rotation == originalRotation &&
                ghost.transform.localScale == originalScale, "Contact lookup moved or rescaled the ghost");
            Require(failures.Count == 0, string.Join("; ", failures));
            Debug.Log("BUILDWORKS_CONTACT_REGRESSIONS_OK narrow support / inside origin / absent support");
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            UnityEditor.EditorApplication.Exit(1);
        }
        finally
        {
            foreach (GameObject item in owned)
                if (item) Object.DestroyImmediate(item);
        }
    }

    private static void VerifyContacts(bool includeStorage)
    {
        try
        {
            VerifyNativePitchContact();
            VerifyContactFailureReasons();
            VerifyNativeMeshEnvelopeContact();
            VerifyUnknownEnvelopeContact();
            if (includeStorage) NativeStorageSerializationAcceptance.Run();
            Debug.Log("BUILDWORKS_CONTACT_OK " + Run());
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            UnityEditor.EditorApplication.Exit(1);
        }
    }

    internal static IEnumerable VerifyIsolatedStructuralQueries()
    {
        var localScene = SceneManager.CreateScene("BuildWorksStructuralQuery", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        Require(localScene.GetPhysicsScene() != Physics.defaultPhysicsScene, "Structural query scene shares world physics");
        var queryObject = new GameObject("IsolatedStructuralQuery");
        var shapeObject = new GameObject("IsolatedStructuralShape");
        GameObject meshSource = null;
        AsyncOperation unloading = null;
        try
        {
            SceneManager.MoveGameObjectToScene(queryObject, localScene);
            SceneManager.MoveGameObjectToScene(shapeObject, localScene);
            queryObject.transform.position = new Vector3(7000, 80, 7000);
            shapeObject.transform.position = new Vector3(7000, 80, 7000);
            shapeObject.transform.localScale = new Vector3(2, 1, .5f);
            var query = queryObject.AddComponent<BoxCollider>(); query.size = Vector3.one * 2.3f;
            var box = shapeObject.AddComponent<BoxCollider>();
            Vector3 virtualPose = new Vector3(10, 0, 0);
            Require(Physics.ComputePenetration(query, virtualPose, Quaternion.identity,
                    box, virtualPose, Quaternion.identity, out _, out _),
                "Isolated boxes cannot compute hypothetical overlap");
            Require(!Physics.ComputePenetration(query, virtualPose, Quaternion.identity,
                    box, virtualPose + Vector3.right * 4, Quaternion.identity, out _, out _),
                "Isolated hypothetical boxes reported a false overlap");
            Vector3 nearest = Physics.ClosestPoint(virtualPose + Vector3.right * 2, box, virtualPose, Quaternion.identity);
            Require(Vector3.Distance(nearest, virtualPose + Vector3.right) < .0001f,
                "Isolated box closest point lost its scale or hypothetical pose");
            nearest = Physics.ClosestPoint(virtualPose + Vector3.right * 2, box, virtualPose, Quaternion.Euler(0, 90, 0));
            Require(Vector3.Distance(nearest, virtualPose + Vector3.right * .25f) < .0001f,
                "Isolated rotated box closest point is incorrect");
            Object.DestroyImmediate(box);
            meshSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = meshSource.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(meshSource);
            var meshCollider = shapeObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh; meshCollider.convex = true;
            nearest = Physics.ClosestPoint(virtualPose + Vector3.right * 2, meshCollider, virtualPose, Quaternion.identity);
            Require(Vector3.Distance(nearest, virtualPose + Vector3.right) < .0001f,
                "Isolated convex mesh closest point lost scale or pose");
            meshCollider.convex = false;
            var hits = new Collider[4];
            int hitCount = localScene.GetPhysicsScene().OverlapBox(shapeObject.transform.position,
                Vector3.one * 1.15f, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            Require(Array.IndexOf(hits, meshCollider, 0, hitCount) >= 0,
                "Isolated native box query missed the enclosed non-convex mesh");
            Require(meshCollider.Raycast(new Ray(shapeObject.transform.position + Vector3.up * 2, Vector3.down),
                    out RaycastHit hit, 3) && Mathf.Abs(hit.point.y - 80.5f) < .0001f,
                "Isolated non-convex mesh cannot query its downward support point");
            Require(queryObject.scene == localScene && shapeObject.scene == localScene &&
                shapeObject.transform.position == new Vector3(7000, 80, 7000) &&
                Physics.OverlapBox(shapeObject.transform.position, Vector3.one * 3).Length == 0,
                "Isolated geometry query changed poses or inserted physical scene colliders");
            Debug.Log("BUILDWORKS_ISOLATED_STRUCTURAL_QUERIES_OK");
        }
        finally
        {
            if (meshSource) Object.DestroyImmediate(meshSource);
            Object.DestroyImmediate(queryObject); Object.DestroyImmediate(shapeObject);
            if (localScene.IsValid()) unloading = SceneManager.UnloadSceneAsync(localScene);
        }
        while (unloading != null && !unloading.isDone) yield return null;
        Require(!localScene.IsValid(), "Structural query scene was not unloaded");
    }

    private static void VerifyUnknownEnvelopeContact()
    {
        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Vector3 origin = new Vector3(4100, 0, 4100);
            ghost.name = "UnknownCornerBox"; ghost.layer = 2;
            ghost.transform.position = origin; ghost.AddComponent<WearNTear>();
            target.name = "UnknownNonconvexCorner"; target.layer = 0;
            target.transform.position = origin + Vector3.one * .62f;
            target.transform.localScale = Vector3.one * .02f;
            Mesh mesh = target.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(target.GetComponent<BoxCollider>());
            var collider = target.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = false;
            Physics.SyncTransforms();
            Require(Array.IndexOf(Physics.OverlapBox(origin, Vector3.one * .65f, Quaternion.identity,
                1, QueryTriggerInteraction.Ignore), collider) >= 0, "Nonconvex fixture misses the native envelope");
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Unknown envelope bounds");
            Require(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1,
                origin + new Vector3(0, 3, -4), 8, PlacementContactSides.All, out _,
                out PlacementContactFailure failure) && failure != PlacementContactFailure.NoContact &&
                (failure & PlacementContactFailure.UnknownGeometry) != 0,
                "Seen nonconvex support was falsely classified as a clean deferrable miss");
            Debug.Log("BUILDWORKS_NATIVE_ENVELOPE_UNKNOWN_OK nonconvex corner never becomes clean NoContact");
        }
        finally { Object.DestroyImmediate(ghost); Object.DestroyImmediate(target); }
    }

    private static void VerifyNativeMeshEnvelopeContact()
    {
        var ghost = new GameObject("RecordedChestEnvelope");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject blocker = null;
        try
        {
            ghost.layer = 2;
            ghost.transform.SetPositionAndRotation(new Vector3(2562.69946f, 41.4991074f, 141.301392f),
                new Quaternion(0, .923879564f, 0, .382683516f));
            var wear = ghost.AddComponent<WearNTear>();
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = "RecordedConvexChestBounds"; child.layer = 2;
            child.transform.SetParent(ghost.transform, false);
            child.transform.localPosition = new Vector3(2.000824e-8f, .3695229f, .008395266f);
            child.transform.localRotation = Quaternion.Euler(0, 180, 0);
            child.transform.localScale = new Vector3(1.82746f, .820531964f, .838022f) * .9f;
            Mesh mesh = child.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(child.GetComponent<BoxCollider>());
            var collider = child.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = true;
            wall.name = "RecordedWallSupport"; wall.layer = 0;
            wall.transform.SetPositionAndRotation(new Vector3(2562.47363f, 42.0993881f, 142.842941f),
                new Quaternion(0, .925540507f, 0, -.378648609f));
            wall.transform.localScale = new Vector3(2, 2, .3f);
            var support = wall.AddComponent<WearNTear>();
            Physics.SyncTransforms();
            Require(Array.IndexOf(Physics.OverlapBox(collider.bounds.center,
                collider.bounds.extents + Vector3.one * .15f, Quaternion.identity, 1,
                QueryTriggerInteraction.Ignore), wall.GetComponent<Collider>()) >= 0,
                "Recorded chest misses the actual native support envelope");
            Vector3 position = ghost.transform.position, scale = ghost.transform.localScale;
            Quaternion rotation = ghost.transform.rotation;
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Chest envelope bounds");
            Vector3 eye = new Vector3(2562.17358f, 42.9073868f, 138.255814f);
            Require(BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 8,
                PlacementContactSides.All, out RaycastHit hit) && hit.collider.gameObject == wall,
                "Recorded WNT mesh edge support was missed");
            Require(Mathf.Abs(hit.normal.y) < .001f && hit.normal.sqrMagnitude > .99f &&
                wall.GetComponent<Collider>().Raycast(new Ray(hit.point + hit.normal * .01f, -hit.normal),
                    out RaycastHit repeated, .021f) && Vector3.Distance(repeated.point, hit.point) < .001f,
                "Mesh fallback supplied an invented surface point/normal");
            Require(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye + Vector3.left * 30, 8,
                PlacementContactSides.All, out _, out PlacementContactFailure failure) &&
                (failure & PlacementContactFailure.OutOfRange) != 0, "Mesh fallback bypassed native eye range");
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "EnclosingRigidbody";
            blocker.layer = 0; blocker.transform.position = collider.bounds.center;
            blocker.transform.localScale = collider.bounds.size + Vector3.one * .4f;
            blocker.AddComponent<Rigidbody>().isKinematic = true;
            Physics.SyncTransforms();
            Require(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 8,
                PlacementContactSides.All, out _, out failure) &&
                (failure & PlacementContactFailure.Obstructed) != 0,
                "Mesh fallback looked through an enclosing rigidbody");
            Object.DestroyImmediate(blocker); blocker = null; Physics.SyncTransforms();
            support.m_supports = false;
            Require(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 8,
                PlacementContactSides.All, out _), "A non-supporting chest became structural support");
            support.m_supports = true;
            var body = wall.AddComponent<Rigidbody>(); body.isKinematic = true;
            Physics.SyncTransforms();
            Require(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 8,
                PlacementContactSides.All, out _), "A rigidbody became mesh support");
            Object.DestroyImmediate(body); Physics.SyncTransforms();
            Object.DestroyImmediate(wear);
            Require(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 8,
                PlacementContactSides.All, out _), "Native WNT envelope fallback leaked into non-WNT mesh");
            Require(ghost.transform.position == position && ghost.transform.rotation == rotation &&
                ghost.transform.localScale == scale, "Mesh support lookup changed the proposed pose");
            Debug.Log("BUILDWORKS_NATIVE_MESH_CONTACT_OK recorded edge / real normal / range / non-support / RB / non-WNT");
        }
        finally
        {
            if (blocker) Object.DestroyImmediate(blocker);
            Object.DestroyImmediate(ghost); Object.DestroyImmediate(wall);
        }
    }

    private static void VerifyContactFailureReasons()
    {
        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Vector3 origin = new Vector3(7000, 0, 7000);
            ghost.layer = 2;
            ghost.transform.position = origin + Vector3.up * .5f;
            floor.layer = 0;
            floor.transform.position = origin + Vector3.down * .5f;
            floor.transform.localScale = new Vector3(8, 1, 8);
            Physics.SyncTransforms();
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Failure fixture bounds");
            bool Probe(float range, int mask, out PlacementContactFailure reason) =>
                BuildWorksPlacementValidation.TryFindContact(ghost, bounds, mask, origin + new Vector3(0, 3, -4),
                    range, PlacementContactSides.Below, out _, out reason);
            Require(Probe(50, 1, out var reason) && reason == PlacementContactFailure.None, "Valid contact reason");
            Require(!Probe(.1f, 1, out reason) && (reason & PlacementContactFailure.OutOfRange) != 0 &&
                reason != PlacementContactFailure.NoContact, "Out-of-range support was called missing support");
            Require(!Probe(50, 0, out reason) && reason == PlacementContactFailure.InvalidQuery, "Invalid mask reason");
            Require(!Probe(float.NaN, 1, out reason) && reason == PlacementContactFailure.InvalidQuery, "Invalid distance reason");
            var body = floor.AddComponent<Rigidbody>(); body.isKinematic = true;
            Require(!Probe(50, 1, out reason) && (reason & PlacementContactFailure.Obstructed) != 0,
                "Rigidbody blocker was called missing support");
            floor.SetActive(false);
            Physics.SyncTransforms();
            Require(!Probe(50, 1, out reason) && reason == PlacementContactFailure.NoContact, "Air contact reason");
            ghost.GetComponent<BoxCollider>().enabled = false;
            var sphere = ghost.AddComponent<SphereCollider>();
            Physics.SyncTransforms();
            Require(!Probe(50, 1, out reason) && (reason & PlacementContactFailure.UnknownGeometry) != 0,
                "Unsupported shape was called missing support");
            Object.DestroyImmediate(sphere);
            var mesh = ghost.AddComponent<MeshCollider>();
            mesh.sharedMesh = ghost.GetComponent<MeshFilter>().sharedMesh; mesh.convex = true;
            Physics.SyncTransforms();
            Require(!Probe(50, 1, out reason) && (reason & PlacementContactFailure.UnknownGeometry) != 0,
                "Mesh envelope miss was called proven missing support");
            Object.DestroyImmediate(mesh);
            var flat = ghost.GetComponent<BoxCollider>(); flat.enabled = true; flat.size = new Vector3(1, 0, 1);
            Physics.SyncTransforms();
            Require(!Probe(50, 1, out reason) && (reason & PlacementContactFailure.UnknownGeometry) != 0,
                "Degenerate box miss was called proven missing support");
        }
        finally { Object.DestroyImmediate(ghost); Object.DestroyImmediate(floor); }
    }

    private static void VerifyNativePitchContact()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject ceiling = null;
        GameObject sideSupport = null;
        try
        {
            Vector3 origin = new Vector3(6000, 0, 6000);
            floor.name = "NativePitchFloor"; floor.layer = 0;
            floor.transform.position = origin + Vector3.down * .5f;
            floor.transform.localScale = new Vector3(8, 1, 8);
            ghost.layer = 2;
            // Measured wood_wall_roof_top_45 collider at the owner's actual test pose.
            ghost.transform.SetPositionAndRotation(origin + Vector3.up * 1.264069f,
                new Quaternion(.074657814f, -.906127515f, -.375330338f, -.180239796f).normalized);
            var box = ghost.GetComponent<BoxCollider>();
            box.size = new Vector3(.400000354f, .600000523f, 2.83604813f);
            ghost.AddComponent<WearNTear>();
            Physics.SyncTransforms();
            Require(box.bounds.min.y > .048f && box.bounds.min.y < .051f, "Native roof fixture gap");
            Require(Physics.BoxCast(ghost.transform.position + Vector3.up * .151f, box.size * .5f,
                Vector3.down, out RaycastHit control, ghost.transform.rotation, .302f, 1,
                QueryTriggerInteraction.Ignore) && control.collider.gameObject == floor,
                "Native roof control sweep missed the real floor");
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Native roof bounds");
            Vector3 eye = origin + new Vector3(0, 3, -4);
            Require(BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 50,
                PlacementContactSides.Below, out RaycastHit hit) && hit.collider.gameObject == floor &&
                hit.normal.y > .999f && Vector3.Distance(hit.point, box.ClosestPoint(hit.point)) <= .1505f,
                "Native pitched roof corner contact was missed or invented");
            floor.GetComponent<Collider>().enabled = false;
            Physics.SyncTransforms();
            Require(!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 50,
                PlacementContactSides.Below, out _), "Native roof was accepted without a floor");
            Require(ghost.transform.position == origin + Vector3.up * 1.264069f,
                "Contact query moved the native roof fixture");
            floor.GetComponent<Collider>().enabled = true;
            ghost.transform.SetPositionAndRotation(origin + Vector3.up * .5f, Quaternion.identity);
            box.size = Vector3.one;
            ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.layer = 0;
            ceiling.transform.position = origin + Vector3.up * 1.5f;
            ceiling.transform.localScale = new Vector3(8, 1, 8);
            Physics.SyncTransforms();
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out bounds) &&
                BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 50,
                    PlacementContactSides.Below, out hit) && hit.collider.gameObject == floor,
                "A tight ceiling hid the valid floor contact");
            ceiling.SetActive(false);
            ghost.transform.position = origin + Vector3.up * .55f;
            sideSupport = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideSupport.layer = 0;
            sideSupport.transform.position = origin + new Vector3(.55f, .55f, .2f);
            sideSupport.transform.localScale = new Vector3(.1f, 1, .04f);
            Physics.SyncTransforms();
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out bounds) &&
                BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye, 50,
                    PlacementContactSides.All, out hit) && hit.collider.gameObject == floor && hit.normal.y > .999f,
                "Extra box contact replaced the accepted floor normal with a side wall");
        }
        finally
        {
            if (ceiling) Object.DestroyImmediate(ceiling);
            if (sideSupport) Object.DestroyImmediate(sideSupport);
            Object.DestroyImmediate(ghost);
            Object.DestroyImmediate(floor);
        }
    }

    internal static string Run()
    {
        var owned = new List<GameObject>();
        GameObject Cube(string name, Vector3 position, Vector3 scale)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.layer = 0;
            result.transform.SetPositionAndRotation(position, Quaternion.identity);
            result.transform.localScale = scale;
            owned.Add(result);
            return result;
        }
        bool Contact(GameObject ghost, PlacementContactSides sides, out RaycastHit hit,
            Vector3? eye = null, float maximumDistance = 50f)
        {
            Physics.SyncTransforms();
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Missing scaled bounds");
            return BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1,
                eye ?? new Vector3(0, 3, -4), maximumDistance, sides, out hit);
        }
        try
        {
            // This fixture occupies a private distant area; unrelated Workbench
            // geometry must never serve as support for a missing-contact test.
            Vector3 origin = new Vector3(2000, 0, 2000);
            GameObject ghost = Cube("ContactGhost", origin + Vector3.up * .5f, Vector3.one);
            Vector3 eye = origin + new Vector3(0, 3, -4);
            GameObject floor = Cube("ContactFloor", origin - Vector3.up * .5f, new Vector3(8,1,8));
            Require(Contact(ghost, PlacementContactSides.Below, out RaycastHit hit, eye) &&
                hit.collider.gameObject == floor && Vector3.Dot(hit.normal, Vector3.up) > .999f &&
                Mathf.Abs(hit.point.y) < .0001f, "Floor contact/normal is not under the frozen part");
            Require(!Contact(ghost, PlacementContactSides.Below, out _, origin + Vector3.up * 100, 5),
                "Contact bypasses native eye distance");
            ghost.transform.position += Vector3.up * .07f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye), "Seven-centimetre fixture gap accepted");
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds gapBounds), "Missing gap bounds");
            Vector3 beforeDiagnostic = ghost.transform.position;
            string diagnosis = BuildWorksTestPlacementDiagnostics.Describe(ghost, gapBounds, 1, eye, 5,
                PlacementContactSides.Below, false);
            int gapStart = diagnosis.IndexOf(" gap=", StringComparison.Ordinal) + 5;
            int gapEnd = diagnosis.IndexOf(' ', gapStart);
            Require(gapStart >= 5 && gapEnd > gapStart &&
                float.TryParse(diagnosis.Substring(gapStart, gapEnd - gapStart),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float measuredGap) &&
                Mathf.Abs(measuredGap - .07f) < .001f && diagnosis.Contains("collider=ContactFloor") &&
                ghost.transform.position == beforeDiagnostic &&
                !Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Diagnostic failed to observe actual floor gap or changed the placement decision");
            WearNTear supportMarker = ghost.AddComponent<WearNTear>();
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.collider.gameObject == floor && ghost.transform.position == beforeDiagnostic,
                "WearNTear collider support envelope rejected a seven-centimetre gap or moved the part");
            Renderer renderer = ghost.GetComponent<Renderer>();
            Bounds visual = renderer.localBounds;
            renderer.localBounds = new Bounds(Vector3.up * 10, Vector3.one * 4);
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.collider.gameObject == floor, "Collision support was still derived from visible mesh bounds");
            renderer.localBounds = visual;
            ghost.transform.position = origin + Vector3.up * .66f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "WearNTear support envelope reached a surface more than .15m away");
            ghost.transform.position = beforeDiagnostic;
            ghost.SetActive(false);
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.collider.gameObject == floor, "Inactive retry lost local collision shape");
            ghost.transform.position = origin + Vector3.up * .9f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Inactive retry reused stale collider bounds after pose change");
            ghost.SetActive(true);

            BoxCollider rootCollider = ghost.GetComponent<BoxCollider>();
            rootCollider.enabled = false;
            ghost.transform.position = beforeDiagnostic;
            var compound = new List<GameObject>();
            foreach (float x in new[] { -3f, 3f })
            {
                var child = new GameObject("ContactCompound");
                child.transform.SetParent(ghost.transform, false);
                child.transform.localPosition = Vector3.right * x;
                child.AddComponent<BoxCollider>().size = Vector3.one;
                compound.Add(child);
            }
            floor.transform.localScale = new Vector3(.25f,1,.25f);
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Compound shapes invented a supporting face between two colliders");
            foreach (GameObject child in compound) Object.DestroyImmediate(child);
            rootCollider.enabled = true;
            ghost.transform.localScale = new Vector3(4,1,.1f);
            ghost.transform.rotation = Quaternion.Euler(0,45,0);
            floor.transform.position = origin + new Vector3(1.16f,-.5f,1.16f);
            floor.transform.localScale = new Vector3(.2f,1,.2f);
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Empty corner of a rotated collider's world AABB supplied contact");
            ghost.transform.localScale = Vector3.one;
            ghost.transform.rotation = Quaternion.identity;
            floor.transform.SetPositionAndRotation(origin - Vector3.up * .5f, Quaternion.identity);
            floor.transform.localScale = new Vector3(8,1,8);
            rootCollider.center = Vector3.up * 3;
            SphereCollider unhandled = ghost.AddComponent<SphereCollider>();
            ghost.transform.position = origin + Vector3.up * .5f;
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.collider.gameObject == floor, "Mixed shape prefab lost strict visual contact");
            ghost.transform.position = beforeDiagnostic;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Mixed shape prefab inherited an unverified .15m envelope");
            Object.DestroyImmediate(unhandled);
            rootCollider.center = Vector3.zero;
            Object.DestroyImmediate(supportMarker);
            ghost.transform.position = origin + Vector3.up * 3;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Unsupported part guesses the distant ground as contact");

            ghost.transform.position = origin + Vector3.up * .5f;
            GameObject ceiling = Cube("ContactCeiling", origin + Vector3.up * 1.5f, new Vector3(8,1,8));
            Require(Contact(ghost, PlacementContactSides.Above, out hit, eye) &&
                hit.collider.gameObject == ceiling && Vector3.Dot(hit.normal, Vector3.down) > .999f,
                "Ceiling contact does not retain the actual underside normal");
            Require(Contact(ghost, PlacementContactSides.NonFloor, out hit, eye) &&
                hit.collider.gameObject == ceiling, "Native notOnFloor incorrectly excludes a ceiling");
            ceiling.SetActive(false);
            GameObject wall = Cube("ContactWall", origin + new Vector3(1, .5f, 0), new Vector3(1,1,8));
            Require(Contact(ghost, PlacementContactSides.NonFloor, out hit, eye) &&
                hit.collider.gameObject == wall && Mathf.Abs(hit.normal.y) < .0001f,
                "Wall-only placement resolves to a floor/ceiling instead");
            wall.SetActive(false);
            floor.transform.position += Vector3.up * .2f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "A surface deep inside the bounds was accepted as a boundary contact");
            ghost.transform.localScale = new Vector3(.01f,1,1);
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Thin collider hid deep floor penetration behind its narrow side");
            supportMarker = ghost.AddComponent<WearNTear>();
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "WearNTear thin collider accepted penetration beyond .15m");
            floor.transform.position += Vector3.up * .2f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "WearNTear thin collider accepted .4m penetration");
            ghost.GetComponent<BoxCollider>().enabled = false;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Strict visual fallback lost directional penetration rejection");
            ghost.GetComponent<BoxCollider>().enabled = true;
            ghost.transform.localScale = Vector3.one;
            Object.DestroyImmediate(supportMarker);
            floor.transform.position -= Vector3.up * .4f;

            floor.AddComponent<Rigidbody>().isKinematic = true;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "A rigidbody was accepted as native static support");
            Object.DestroyImmediate(floor.GetComponent<Rigidbody>());
            floor.SetActive(false);
            Require(!Contact(ghost, PlacementContactSides.All, out _, eye),
                "The ghost's own collider was accepted as support");
            floor.SetActive(true);

            ghost.transform.localScale = Vector3.one * .01f;
            ghost.transform.position = origin + Vector3.up * .005f;
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.collider.gameObject == floor, "One-percent furniture lost its contact");
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds tiny) &&
                Mathf.Abs(tiny.size.y - .01f) < .00001f, "One-percent bounds were clamped to full size");
            ghost.transform.position += Vector3.up * .01f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "One-percent contact tolerance reaches an unrelated lower surface");
            supportMarker = ghost.AddComponent<WearNTear>();
            Require(Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Native support envelope was incorrectly scaled down with one-percent furniture");
            ghost.transform.position += Vector3.up * .15f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Tiny native support envelope exceeded .15m in world space");
            Object.DestroyImmediate(supportMarker);

            ghost.transform.localScale = new Vector3(2, .5f, 1);
            ghost.transform.SetPositionAndRotation(origin + Vector3.up, Quaternion.Euler(0,90,0));
            ghost.SetActive(false);
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds inactive) &&
                Vector3.Distance(inactive.size, new Vector3(1,.5f,2)) < .001f &&
                Vector3.Distance(inactive.center, ghost.transform.position) < .001f,
                "Inactive retry ghost bounds ignore its current rotation/base scale");
            ghost.SetActive(true);

            // Keep an actual sloping surface normal; the native notOnTiltingSurface
            // branch will decide whether that normal is legal for a particular Piece.
            floor.transform.rotation = Quaternion.Euler(0,0,40);
            floor.transform.position = origin - Vector3.up * .5f;
            ghost.transform.localScale = Vector3.one;
            ghost.transform.SetPositionAndRotation(origin + Vector3.up * (.5f / Mathf.Cos(40 * Mathf.Deg2Rad)),
                Quaternion.identity);
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.normal.y > .7f && hit.normal.y < .8f,
                "Sloping support normal was replaced with Vector3.up");
            return "Actual Physics contact: floor/ceiling/wall normals; no distant support; native eye range; " +
                "rigidbody and ghost exclusions; 1% scale; inactive rotated bounds; slope normal; " +
                "WearNTear collider envelope, independent visual bounds, compound/rotated gaps passed. " +
                "Host Piece flags, water-volume queries, station checks and costs require the native integration contract separately.";
        }
        finally
        {
            foreach (GameObject item in owned)
                if (item) { item.SetActive(false); Object.DestroyImmediate(item); }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Placement contact: " + message);
    }
}

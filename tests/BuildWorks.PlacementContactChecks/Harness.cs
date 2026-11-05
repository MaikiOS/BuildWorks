using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OstrixMods.BuildWorks;
using H = ContactHost;

// Execute the production contact algorithm with instrumented host objects/ray results.
// Unity Vector3/Bounds/Ray math is real; Transform, Quaternion and Physics are substitutes.
internal static class Program
{
    private static int checks;
    private static void Require(bool result, string message)
    { ++checks; if (!result) throw new InvalidOperationException(message); }

    private static int Main()
    {
        try
        {
            foreach (long usage in new long[] { 2, 128, 1024, 1152, 1154 })
                Require(BuildWorksPlacementValidation.UsesSurfaceAttachment((Piece.UsageTagFlags)usage),
                    "Observed furniture/storage/crafting usage was rejected: " + usage);
            foreach (long usage in new long[] { 0, 20, 36, 68, 2048, 128 | 16, 2 | 32, 1024 | 131072 })
                Require(!BuildWorksPlacementValidation.UsesSurfaceAttachment((Piece.UsageTagFlags)usage),
                    "Structural, transport, mixed or unknown usage obtained attachment: " + usage);
            var ghost = Cube(new Vector3(0, .57f, 0));
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            var floor = new H.Collider { gameObject = new H.GameObject() };
            Plane(floor);
            Require(Contact(ghost, out H.RaycastHit hit) && hit.collider == floor,
                "REPRO: WearNTear shape7cm above real support rejected by visual1% gate.");
            Require(!SurfaceContact(ghost, out _), "REPRO: functional object floating7cm above support accepted as contact.");
            ghost.transform.position = Vector3.up * .5f;
            Require(SurfaceContact(ghost, out var strictFailure) && strictFailure == PlacementContactFailure.None,
                "Exact functional box contact rejected.");
            VerifyAttachmentSupports(ghost, floor);
            ghost.transform.position = Vector3.up * .503f;
            Require(!SurfaceContact(ghost, out strictFailure) && strictFailure == PlacementContactFailure.NoContact,
                "Functional3mm gap accepted as contact.");
            ghost.transform.position = Vector3.up * .5f;
            floor.isTrigger = true;
            Require(!SurfaceContact(ghost, out _), "Trigger-only surface granted functional attachment.");
            floor.isTrigger = false;
            var unsupported = new H.Collider { gameObject = ghost };
            ghost.Components.Add(unsupported);
            Require(!SurfaceContact(ghost, out strictFailure) && (strictFailure & PlacementContactFailure.UnknownGeometry) != 0,
                "A convenient box hid an unsupported member of the functional compound.");
            ghost.Components.Remove(unsupported);
            var visualOnly = Cube(Vector3.up * .5f);
            visualOnly.Components.Remove(visualOnly.GetComponent<H.BoxCollider>());
            Require(!SurfaceContact(visualOnly, out strictFailure) && (strictFailure & PlacementContactFailure.UnknownGeometry) != 0,
                "Visual bounds granted attachment without physical colliders.");
            var large = Cube(Vector3.up * .525f);
            large.transform.localScale = new Vector3(10, 1, 10);
            Require(!SurfaceContact(large, out _), "Functional contact tolerance grew with furniture size.");
            ghost.transform.position = Vector3.up * .57f;
            H.Transform pose = ghost.transform;
            Vector3 before = pose.position;
            ghost.GetComponent<H.Renderer>().localBounds = new Bounds(Vector3.up * 10, Vector3.one * 4);
            Require(Contact(ghost, out _) && pose.position == before,
                "Visual bounds changed contact or algorithm moved planned pose.");
            pose.position = Vector3.up * .66f;
            Require(!Contact(ghost, out _), "Surface outside native support envelope accepted.");
            pose.position = Vector3.up * .57f;
            ghost.activeSelf = false;
            Require(Contact(ghost, out _), "Inactive retry lost local collider geometry.");
            pose.position = Vector3.up * .9f;
            Require(!Contact(ghost, out _), "Inactive retry reused stale collider pose.");
            ghost.activeSelf = true;
            pose.localScale = new Vector3(2, .5f, 1);
            pose.rotation = H.Quaternion.Yaw(90);
            pose.position = new Vector3(8, .32f, -5);
            Require(Contact(ghost, out _) && pose.position.y == .32f,
                "Nonuniform rotated collider lost support or changed position.");

            ghost = Cube(Vector3.up * .57f);
            Require(!Contact(ghost, out _), "Non-WearNTear object inherited support envelope.");
            ghost.transform.position = Vector3.up * .5f;
            Require(Contact(ghost, out _), "Ordinary exact collider contact regressed.");
            Require(!Contact(ghost, out _, Vector3.up * 8, 8), "Native exclusive eye range bypassed.");
            floor.attachedRigidbody = new H.HostObject();
            Require(!Contact(ghost, out _), "Native rigidbody exclusion bypassed.");
            floor.attachedRigidbody = null;

            ghost = Cube(Vector3.up * .5f);
            ghost.transform.localScale = new Vector3(.01f,1,1);
            Plane(floor, y: .2f);
            Require(!Contact(ghost, out _), "Thin shape hid .2m penetration behind its narrow side.");
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            Require(!Contact(ghost, out _), "WearNTear thin shape accepted penetration beyond .15m.");
            Plane(floor, y: .4f);
            Require(!Contact(ghost, out _), "WearNTear thin shape hid .4m penetration.");
            ghost.Components.Remove(ghost.GetComponent<H.BoxCollider>());
            Require(!Contact(ghost, out _), "Strict visual fallback changed directional boundary distance.");
            ghost = Cube(Vector3.up * .5f);
            Plane(floor);
            ghost.GetComponent<H.BoxCollider>().size = new Vector3(float.NaN,1,1);
            Require(!Contact(ghost, out _), "Invalid known collision shape silently fell back to visual bounds.");
            ghost.GetComponent<H.BoxCollider>().size = Vector3.zero;
            Require(!Contact(ghost, out _), "Empty known collision shape silently fell back to visual bounds.");
            ghost = Cube(Vector3.up * .5f);

            ghost.Components.Clear();
            ghost.Components.Add(new H.Renderer { gameObject = ghost, localBounds = new Bounds(Vector3.zero, Vector3.one) });
            Require(Contact(ghost, out _), "Legacy collider-less contact changed.");
            ghost.transform.position = Vector3.up * .57f;
            Require(!Contact(ghost, out _), "Legacy fallback gained unverified broad tolerance.");

            ghost = Cube(Vector3.up * .57f);
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            ghost.GetComponent<H.BoxCollider>().center = Vector3.up * 3;
            ghost.Components.Add(new H.Collider { gameObject = ghost });
            ghost.transform.position = Vector3.up * .5f;
            Require(Contact(ghost, out _), "Mixed known/unhandled shapes lost legacy exact visual contact.");
            ghost.transform.position = Vector3.up * .57f;
            Require(!Contact(ghost, out _), "Mixed shape fallback gained an unverified support envelope.");
            ghost.transform.position = Vector3.up * .5f;
            ghost.GetComponent<H.BoxCollider>().size = Vector3.zero;
            Require(!Contact(ghost, out _), "Unhandled shape hid a malformed known collider.");

            ghost = Cube(Vector3.up * .57f);
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            var box = ghost.GetComponent<H.BoxCollider>();
            box.center = Vector3.right * -3;
            var other = new H.BoxCollider { gameObject = ghost, center = Vector3.right * 3, size = Vector3.one };
            ghost.Components.Add(other);
            ghost.GetComponent<H.Renderer>().localBounds = new Bounds(Vector3.zero, new Vector3(7,1,1));
            Plane(floor, point => Mathf.Abs(point.x) < .3f);
            Require(!Contact(ghost, out _), "Combined collider bounds invented support in compound gap.");

            ghost = Cube(Vector3.up * .57f);
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            ghost.transform.localScale = new Vector3(4,1,.1f);
            ghost.transform.rotation = H.Quaternion.Yaw(45);
            Plane(floor, point => point.x > 1 && point.z > 1);
            Require(!Contact(ghost, out _), "Empty rotated world-AABB corner became physical support.");

            ghost = Cube(Vector3.up * .57f);
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            ghost.GetComponent<H.BoxCollider>().center = Vector3.up * 3;
            var branch = Cube(Vector3.zero);
            branch.transform.parent = ghost.transform;
            branch.activeSelf = false;
            Plane(floor);
            Require(!Contact(ghost, out _), "Inactive child collider supplied a fake contact.");
            branch.activeSelf = true;
            Require(Contact(ghost, out _), "Active child collider lost contact.");
            branch.GetComponent<H.BoxCollider>().isTrigger = true;
            Require(!Contact(ghost, out _), "Trigger supplied structural contact.");
            branch.GetComponent<H.BoxCollider>().isTrigger = false;
            branch.GetComponent<H.BoxCollider>().enabled = false;
            Require(!Contact(ghost, out _), "Disabled collider supplied structural contact.");

            ghost = Cube(Vector3.up * .57f);
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            ghost.Components.Remove(ghost.GetComponent<H.BoxCollider>());
            ghost.Components.Add(new H.MeshCollider { gameObject = ghost,
                sharedMesh = new H.Mesh { bounds = new Bounds(Vector3.zero, Vector3.one) } });
            ghost.activeSelf = false;
            Require(Contact(ghost, out _), "Inactive mesh lost recomputed local bounds.");
            ghost.activeSelf = true;
            H.Mesh savedMesh = ghost.GetComponent<H.MeshCollider>().sharedMesh;
            ghost.GetComponent<H.MeshCollider>().sharedMesh = null;
            Require(!Contact(ghost, out _), "Missing mesh shape silently fell back to visual bounds.");
            ghost.GetComponent<H.MeshCollider>().sharedMesh = savedMesh;

            var self = ghost.GetComponent<H.MeshCollider>();
            H.Physics.Query = (ray, reach, mask) => new[] {
                new H.RaycastHit { collider = self, distance = .1f, point = ray.GetPoint(.1f) } };
            Require(!Contact(ghost, out _), "Ghost collider counted as external support.");
            Plane(floor);
            var single = H.Physics.Query;
            var far = new H.Collider { gameObject = new H.GameObject() };
            H.Physics.Query = (ray, reach, mask) => {
                H.RaycastHit[] nearest = single(ray, reach, mask);
                if (nearest.Length == 0) return nearest;
                return Enumerable.Range(0,39).Select(_ => new H.RaycastHit {
                    collider = far, point = nearest[0].point - Vector3.up * .02f,
                    distance = nearest[0].distance + .02f }).Concat(nearest).ToArray();
            };
            Require(Contact(ghost, out hit) && hit.collider == floor && H.Physics.AllCalls > 0,
                "Full ray buffer hid a closer support hit.");
            ghost = Cube(Vector3.up * .57f);
            ghost.Components.Add(new WearNTear { gameObject = ghost });
            H.Physics.Query = (ray, reach, mask) => Array.Empty<H.RaycastHit>();
            int allBefore = H.Physics.AllCalls;
            H.Physics.BoxQuery = (origin, extents, direction, rotation, reach, mask) => direction.y < -.5f
                ? Enumerable.Repeat(new H.RaycastHit { collider = floor, distance = .151f, point = Vector3.zero }, 40).ToArray()
                : Array.Empty<H.RaycastHit>();
            Require(Contact(ghost, out hit) && hit.collider == floor && H.Physics.AllCalls > allBefore,
                "Dense scripted box sweep did not use BoxCastAll.");
            H.Physics.BoxQuery = (a,b,c,d,e,f) => Array.Empty<H.RaycastHit>();
            var solid = Cube(Vector3.down * .5f).GetComponent<H.BoxCollider>();
            bool overlapPhase = false;
            H.Physics.OverlapQuery = (center,extents,rotation,mask) => { overlapPhase = true; return new H.Collider[] { solid }; };
            H.Physics.Query = (ray,reach,mask) => overlapPhase
                ? new[] { new H.RaycastHit { collider = solid, point = Vector3.zero, distance = Vector3.Distance(ray.origin, Vector3.zero) } }
                : Array.Empty<H.RaycastHit>();
            overlapPhase = false; solid.attachedRigidbody = new H.HostObject();
            Require(!AllContact(out var envelopeFailure) && (envelopeFailure & PlacementContactFailure.Obstructed) != 0,
                "Native envelope looked through an overlapping rigidbody.");
            solid.attachedRigidbody = null; overlapPhase = false;
            solid.gameObject.Components.Add(new WearNTear { gameObject = solid.gameObject, m_supports = false });
            Require(!AllContact(out _), "Non-supporting native neighbour became an envelope support.");
            var concave = new H.MeshCollider { gameObject = new H.GameObject(), convex = false };
            H.Physics.OverlapQuery = (a,b,c,d) => new H.Collider[] { concave };
            Require(!AllContact(out envelopeFailure) && (envelopeFailure & PlacementContactFailure.UnknownGeometry) != 0,
                "Nonconvex envelope query guessed an exact surface.");
            Console.WriteLine("PASS CPU production contact algorithm: " + checks +
                " assertions; local collision geometry,7cm support/far refusal,pose/scale,inactive/compound/rotated gaps,range,rigidbody,self,dense rays/scripted box/envelope refusals. Host substitutes; positive envelope/Unity Physics/game/reload not tested.");
            return 0;
            bool AllContact(out PlacementContactFailure failure)
            {
                overlapPhase = false;
                BuildWorksPlacementValidation.TryGetBounds(ghost, out var envelope);
                return BuildWorksPlacementValidation.TryFindContact(ghost, envelope, 1, new Vector3(0,3,-4), 50,
                    PlacementContactSides.All, out _, out failure);
            }
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void VerifyAttachmentSupports(H.GameObject ghost, H.Collider floor)
    {
        var piece = new Piece { gameObject = floor.gameObject, m_usage = Piece.UsageTagFlags.Storage };
        var wear = new WearNTear { gameObject = floor.gameObject, m_supports = false };
        var view = new ZNetView { gameObject = floor.gameObject };
        var container = new Container { gameObject = floor.gameObject };
        floor.gameObject.Components.AddRange(new H.Component[] { piece, wear, view, container });
        Require(!SurfaceContact(ghost, out _), "REPRO: chest-only contact accepted before payment.");
        wear.m_supports = true;
        Require(!SurfaceContact(ghost, out _), "Container became attachment support through its native donor flag.");
        floor.gameObject.Components.Remove(container);
        Require(SurfaceContact(ghost, out _), "Existing native supporting furniture stopped being a donor.");
        piece.m_usage = Piece.UsageTagFlags.Crafting;
        Require(SurfaceContact(ghost, out _), "Existing native supporting crafting object stopped being a donor.");
        piece.m_usage = 0;
        Require(SurfaceContact(ghost, out _), "Legacy wall/roof with unknown usage lost native support eligibility.");
        piece.m_usage = Piece.UsageTagFlags.Wall | Piece.UsageTagFlags.Building;
        Require(SurfaceContact(ghost, out _), "Real building support was rejected.");
        view.Valid = false;
        Require(!SurfaceContact(ghost, out _), "Invalid building network view granted attachment.");
        view.Valid = true;
        wear.m_supports = false;
        Require(!SurfaceContact(ghost, out _), "Non-supporting building granted attachment.");
        wear.m_supports = true;
        var furniture = new H.Collider { gameObject = new H.GameObject() };
        furniture.gameObject.Components.Add(new Piece { gameObject = furniture.gameObject, m_usage = Piece.UsageTagFlags.Storage });
        furniture.gameObject.Components.Add(new WearNTear { gameObject = furniture.gameObject, m_supports = false });
        furniture.gameObject.Components.Add(new ZNetView { gameObject = furniture.gameObject });
        var query = H.Physics.Query;
        H.Physics.Query = (ray, reach, mask) => query(ray, reach, mask)
            .SelectMany(hit => new[] { new H.RaycastHit { collider = furniture, distance = 0, point = hit.point }, hit }).ToArray();
        Require(SurfaceContact(ghost, out _), "Overlapping furniture hid the actual building contact.");
        furniture.attachedRigidbody = new H.HostObject();
        Require(!SurfaceContact(ghost, out _), "Attachment filtering looked through a moving blocker.");
        H.Physics.Query = query;
        floor.gameObject.Components.Clear();
    }

    private static bool SurfaceContact(H.GameObject ghost, out PlacementContactFailure failure)
    {
        BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds);
        return BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, new Vector3(0, 3, -4), 50,
            PlacementContactSides.Below, out _, out failure, surfaceAttachment: true);
    }

    private static H.GameObject Cube(Vector3 position)
    {
        var result = new H.GameObject();
        result.transform.position = position;
        result.Components.Add(new H.Renderer { gameObject = result, localBounds = new Bounds(Vector3.zero, Vector3.one) });
        result.Components.Add(new H.BoxCollider { gameObject = result, size = Vector3.one });
        return result;
    }
    private static bool Contact(H.GameObject ghost, out H.RaycastHit hit, Vector3? eye = null, float range = 50)
    {
        Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Missing visual bounds.");
        return BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1, eye ?? new Vector3(0,3,-4),
            range, PlacementContactSides.Below, out hit);
    }
    private static void Plane(H.Collider floor, Func<Vector3,bool> includes = null, float y = 0)
    {
        H.Physics.Query = (ray, reach, mask) => {
            if (mask != 1 || ray.direction.y >= -.5f) return Array.Empty<H.RaycastHit>();
            float distance = (y - ray.origin.y) / ray.direction.y;
            Vector3 point = ray.GetPoint(distance);
            if (distance < 0 || distance > reach || includes != null && !includes(point)) return Array.Empty<H.RaycastHit>();
            return new[] { new H.RaycastHit { collider = floor, distance = distance, point = point, normal = Vector3.up } };
        };
    }
}

public sealed class Piece : H.Component
{
    public UsageTagFlags m_usage;
    [System.Flags] public enum UsageTagFlags
    { Crafting = 2, Building = 4, Floor = 8, Wall = 16, Roof = 32, Architecture = 64,
      Furniture = 128, Storage = 1024, Transport = 2048, Stairs = 131072, Doors = 262144 }
}

public sealed class WearNTear : H.Component { public bool m_supports = true; }
public sealed class ZNetView : H.Component { public bool Valid = true; public bool IsValid() => Valid; }
public sealed class Container : H.Component { }

namespace ContactHost
{
    public class HostObject { public static implicit operator bool(HostObject value) => value != null; }
    public class Component : HostObject
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : Component
        {
            for (Transform current = transform; current != null; current = current.parent)
                if (current.gameObject.GetComponent<T>() is T value) return value;
            return null;
        }
    }
    public sealed class GameObject : HostObject
    {
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public readonly Transform transform;
        public readonly List<Component> Components = new List<Component>();
        public GameObject() { transform = new Transform { gameObject = this }; }
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component => Components.OfType<T>()
            .Concat(transform.Children.SelectMany(child => child.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public sealed class Transform : HostObject
    {
        public GameObject gameObject;
        public Vector3 position, localScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity;
        private Transform parentValue;
        public readonly List<Transform> Children = new List<Transform>();
        public Transform parent { get => parentValue; set { parentValue?.Children.Remove(this); parentValue = value; value?.Children.Add(this); } }
        public Vector3 lossyScale => parent == null ? localScale : Vector3.Scale(parent.lossyScale, localScale);
        public Vector3 TransformPoint(Vector3 point)
        {
            Vector3 transformed = position + rotation * Vector3.Scale(localScale, point);
            return parent == null ? transformed : parent.TransformPoint(transformed);
        }
        public bool IsChildOf(Transform root) => this == root || parent != null && parent.IsChildOf(root);
    }
    public sealed class Renderer : Component { public bool enabled = true; public Bounds localBounds; }
    public class Collider : Component
    {
        public bool enabled = true, isTrigger;
        public HostObject attachedRigidbody;
        public Func<Vector3, Vector3> ClosestPointQuery;
        public Bounds bounds
        {
            get
            {
                Bounds local = this is BoxCollider box ? new Bounds(box.center, box.size) :
                    this is MeshCollider mesh && mesh.sharedMesh ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
                Bounds result = new Bounds(transform.TransformPoint(local.min), Vector3.zero);
                for (int corner = 1; corner < 8; ++corner)
                    result.Encapsulate(transform.TransformPoint(new Vector3((corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y, (corner & 4) == 0 ? local.min.z : local.max.z)));
                return result;
            }
        }
        public Vector3 ClosestPoint(Vector3 point) => ClosestPointQuery != null ? ClosestPointQuery(point) :
            throw new InvalidOperationException("CPU fixture must supply its surface query; native Unity ClosestPoint is unavailable.");
    }
    public sealed class BoxCollider : Collider { public Vector3 center, size; }
    public sealed class MeshCollider : Collider { public Mesh sharedMesh; public bool convex; }
    public sealed class Mesh : HostObject { public Bounds bounds; }
    public struct RaycastHit { public Collider collider; public Vector3 point, normal; public float distance; }
    public enum QueryTriggerInteraction { UseGlobal, Ignore }
    public static class Physics
    {
        public static Func<Ray,float,int,RaycastHit[]> Query;
        // Scripted query results exercise call boundaries, not Unity collision physics.
        public static Func<Vector3,Vector3,Vector3,Quaternion,float,int,RaycastHit[]> BoxQuery = (a,b,c,d,e,f) => Array.Empty<RaycastHit>();
        public static Func<Vector3,Vector3,Quaternion,int,Collider[]> OverlapQuery = (a,b,c,d) => Array.Empty<Collider>();
        public static int AllCalls;
        public static int RaycastNonAlloc(Ray ray, RaycastHit[] buffer, float reach, int mask, QueryTriggerInteraction triggers)
        {
            RaycastHit[] values = Query(ray, reach, mask);
            int count = Math.Min(values.Length, buffer.Length);
            Array.Copy(values, buffer, count);
            return count;
        }
        public static RaycastHit[] RaycastAll(Ray ray, float reach, int mask, QueryTriggerInteraction triggers)
        { ++AllCalls; return Query(ray, reach, mask); }
        public static int BoxCastNonAlloc(Vector3 origin, Vector3 extents, Vector3 direction, RaycastHit[] buffer,
            Quaternion rotation, float reach, int mask, QueryTriggerInteraction triggers)
        {
            var values = BoxQuery(origin, extents, direction, rotation, reach, mask);
            int count = Math.Min(values.Length, buffer.Length); Array.Copy(values, buffer, count); return count;
        }
        public static RaycastHit[] BoxCastAll(Vector3 origin, Vector3 extents, Vector3 direction, Quaternion rotation,
            float reach, int mask, QueryTriggerInteraction triggers)
        { ++AllCalls; return BoxQuery(origin, extents, direction, rotation, reach, mask); }
        public static Collider[] OverlapBox(Vector3 center, Vector3 extents, Quaternion rotation, int mask, QueryTriggerInteraction triggers)
            => OverlapQuery(center, extents, rotation, mask);
    }
    public struct Quaternion
    {
        private System.Numerics.Quaternion value;
        public static Quaternion identity => new Quaternion { value = System.Numerics.Quaternion.Identity };
        public static Quaternion Yaw(float degrees) => new Quaternion {
            value = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, degrees * (float)Math.PI / 180) };
        public static Quaternion Inverse(Quaternion rotation) => new Quaternion { value = System.Numerics.Quaternion.Inverse(rotation.value) };
        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            var result = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(point.x,point.y,point.z), rotation.value);
            return new Vector3(result.X,result.Y,result.Z);
        }
    }
}

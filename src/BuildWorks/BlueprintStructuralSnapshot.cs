using System;
using System.Collections.Generic;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evaluation = OstrixMods.BuildWorks.Geometry.StructuralSupportEvaluation;

namespace OstrixMods.BuildWorks
{
    // Collider-only copies in a non-simulated physics scene. Never clones gameplay scripts or network state.
    internal sealed class BlueprintStructuralSnapshot : IDisposable
    {
        internal sealed class Part
        {
            internal string Id, UnknownReason;
            internal GameObject Root;
            internal Evaluation.Profile? Profile;
            internal Vector3 ComOffset;
            internal bool Relays, ForceCorrectCom, NoSupportWear, StaticAnchor, GroundContact;
            internal bool CompleteGeometry = true;
            internal Part StaticSupport;
            internal readonly List<Collider> Colliders = new List<Collider>();
            internal GameObject Source;
            internal Collider[] SourceColliders;
            internal Dictionary<Transform, Transform> Transforms;
            internal readonly Dictionary<Collider, Collider> Copies = new Dictionary<Collider, Collider>();
            internal Vector3 Origin => Root.transform.position;
            internal Vector3 Com => Origin + Root.transform.rotation * ComOffset;
        }

        internal readonly struct Contact
        {
            internal Contact(Part source, Vector3 point) { Source = source; Point = point; }
            internal Part Source { get; }
            internal Vector3 Point { get; }
        }

        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type NativeType = typeof(WearNTear);
        private static readonly MethodInfo MaterialProperties = NativeType.GetMethod("GetMaterialProperties", InstanceFields,
            null, new[] { typeof(float).MakeByRefType(), typeof(float).MakeByRefType(),
                typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }, null);
        private static readonly FieldInfo ComOffset = NativeType.GetField("m_comOffset", InstanceFields);
        private static readonly FieldInfo ForceCom = NativeType.GetField("m_forceCorrectCOMCalculation", InstanceFields);
        private static readonly FieldInfo SupportWear = NativeType.GetField("m_noSupportWear", InstanceFields);
        private static readonly FieldInfo RayMask = NativeType.GetField("s_rayMask", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo QueryBuffer = NativeType.GetField("s_tempColliders", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly Scene scene;
        private readonly GameObject root;
        private readonly Dictionary<string, Part> parts = new Dictionary<string, Part>(StringComparer.Ordinal);
        private readonly Dictionary<Collider, Part> owners = new Dictionary<Collider, Part>();
        private readonly Collider[] hits;
        private readonly int rayMask;
        private bool disposed;

        internal BlueprintStructuralSnapshot()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Structural snapshots require Play Mode.");
            rayMask = RayMask?.GetValue(null) is int mask ? mask : 0;
            int capacity = (QueryBuffer?.GetValue(null) as Collider[])?.Length ?? 0;
            hits = new Collider[Math.Max(1, capacity)];
            scene = SceneManager.CreateScene("BuildWorksStructure_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            root = new GameObject("BuildWorksStructuralSnapshot") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        internal IEnumerable<Part> Parts => parts.Values;
        internal AsyncOperation Unloading { get; private set; }
        internal bool IsSceneLoaded => scene.IsValid() && scene.isLoaded;

        internal static bool IsEmptyMesh(Collider collider) => collider is MeshCollider mesh && !mesh.sharedMesh;

        internal Part Add(string id, GameObject source, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(id) || parts.ContainsKey(id)) throw new ArgumentException("Invalid structural part ID.", nameof(id));
            float rotationLength = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            if (!Finite(position) || !Finite(scale) || scale.x <= 0 || scale.y <= 0 || scale.z <= 0 ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w) ||
                !Finite(rotationLength) || rotationLength < .0001f)
                throw new ArgumentOutOfRangeException(nameof(position));
            var part = new Part { Id = id };
            parts.Add(id, part);
            if (!source) { part.UnknownReason = "missing-prefab"; part.CompleteGeometry = false; return part; }
            part.Root = new GameObject("Part_" + id) { layer = source.layer };
            part.Root.SetActive(false);
            part.Root.transform.SetParent(root.transform, false);
            part.Root.transform.SetPositionAndRotation(position, rotation);
            part.Root.transform.localScale = scale;
            CaptureProfile(source, part);
            var transforms = new Dictionary<Transform, Transform> { [source.transform] = part.Root.transform };
            part.Source = source;
            part.SourceColliders = source.GetComponentsInChildren<Collider>(true);
            part.Transforms = transforms;
            try
            {
                foreach (Collider original in part.SourceColliders)
                {
                    if (original.isTrigger || original.attachedRigidbody || IsEmptyMesh(original)) continue;
                    Transform target = CopyTransform(original.transform);
                    Collider copy;
                    if (original is BoxCollider box)
                    {
                        var value = target.gameObject.AddComponent<BoxCollider>();
                        value.center = box.center; value.size = box.size; copy = value;
                    }
                    else if (original is SphereCollider sphere)
                    {
                        var value = target.gameObject.AddComponent<SphereCollider>();
                        value.center = sphere.center; value.radius = sphere.radius; copy = value;
                    }
                    else if (original is CapsuleCollider capsule)
                    {
                        var value = target.gameObject.AddComponent<CapsuleCollider>();
                        value.center = capsule.center; value.radius = capsule.radius;
                        value.height = capsule.height; value.direction = capsule.direction; copy = value;
                    }
                    else if (original is MeshCollider mesh && mesh.sharedMesh)
                    {
                        var value = target.gameObject.AddComponent<MeshCollider>();
                        value.cookingOptions = mesh.cookingOptions;
                        value.sharedMesh = mesh.sharedMesh; value.convex = mesh.convex; copy = value;
                    }
                    else { part.UnknownReason = "unsupported-collider"; part.CompleteGeometry = false; continue; }
                    copy.enabled = original.enabled;
                    part.Colliders.Add(copy);
                    part.Copies.Add(original, copy);
                    owners.Add(copy, part);
                }
                if (part.Colliders.Count == 0) part.UnknownReason = "no-static-collision";
                part.Root.SetActive(true);
            }
            catch
            {
                part.Root.SetActive(false);
                foreach (Collider collider in part.Colliders) owners.Remove(collider);
                part.Colliders.Clear();
                UnityEngine.Object.Destroy(part.Root);
                part.UnknownReason = "collider-copy-failed";
                part.CompleteGeometry = false;
                throw;
            }
            return part;

            Transform CopyTransform(Transform original)
            {
                if (transforms.TryGetValue(original, out Transform result)) return result;
                Transform parent = CopyTransform(original.parent);
                var item = new GameObject(original.name) { layer = original.gameObject.layer };
                result = item.transform; result.SetParent(parent, false);
                result.localPosition = original.localPosition; result.localRotation = original.localRotation;
                result.localScale = original.localScale; item.SetActive(original.gameObject.activeSelf);
                transforms.Add(original, result);
                return result;
            }
        }

        // Refresh collider-only copies without activating the source or creating another physics scene.
        internal bool Refresh(Part part, GameObject source, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            ThrowIfDisposed();
            if (part == null || !source || part.Source != source || !part.Root || !part.CompleteGeometry ||
                !Finite(position) || !Finite(scale) || scale.x <= 0 || scale.y <= 0 || scale.z <= 0 ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w)) return false;
            Collider[] current = source.GetComponentsInChildren<Collider>(true);
            if (current.Length != part.SourceColliders.Length) return false;
            for (int i = 0; i < current.Length; ++i)
                if (current[i] != part.SourceColliders[i] ||
                    (!current[i].isTrigger && !current[i].attachedRigidbody && !IsEmptyMesh(current[i])) != part.Copies.ContainsKey(current[i])) return false;
            // Rebuild a changed capsule axis from fully configured inactive geometry, in the same scene.
            foreach (var pair in part.Copies)
                if (pair.Key is CapsuleCollider capsule && pair.Value is CapsuleCollider copy && capsule.direction != copy.direction) return false;
            part.Root.transform.SetPositionAndRotation(position, rotation);
            part.Root.transform.localScale = scale;
            part.Root.layer = source.layer;
            foreach (var pair in part.Transforms)
            {
                if (!pair.Key || !pair.Value) return false;
                if (pair.Key == source.transform) continue;
                if (!part.Transforms.TryGetValue(pair.Key.parent, out Transform parent) || pair.Value.parent != parent) return false;
                pair.Value.localPosition = pair.Key.localPosition;
                pair.Value.localRotation = pair.Key.localRotation;
                pair.Value.localScale = pair.Key.localScale;
                pair.Value.gameObject.layer = pair.Key.gameObject.layer;
                pair.Value.gameObject.SetActive(pair.Key.gameObject.activeSelf);
            }
            foreach (var pair in part.Copies)
            {
                Collider original = pair.Key, copy = pair.Value;
                if (!original || !copy) return false;
                if (original is BoxCollider box && copy is BoxCollider boxCopy)
                { boxCopy.center = box.center; boxCopy.size = box.size; }
                else if (original is SphereCollider sphere && copy is SphereCollider sphereCopy)
                { sphereCopy.center = sphere.center; sphereCopy.radius = sphere.radius; }
                else if (original is CapsuleCollider capsule && copy is CapsuleCollider capsuleCopy)
                {
                    capsuleCopy.center = capsule.center; capsuleCopy.radius = capsule.radius;
                    capsuleCopy.height = capsule.height;
                }
                else if (original is MeshCollider mesh && copy is MeshCollider meshCopy && mesh.sharedMesh)
                {
                    if (meshCopy.cookingOptions != mesh.cookingOptions) meshCopy.cookingOptions = mesh.cookingOptions;
                    if (meshCopy.convex != mesh.convex) meshCopy.convex = mesh.convex;
                    // Reassign so in-place mesh edits cannot leave the cached collider's cooked shape stale.
                    meshCopy.sharedMesh = mesh.sharedMesh;
                }
                else return false;
                copy.enabled = original.enabled;
            }
            return true;
        }

        internal void Clear()
        {
            ThrowIfDisposed();
            foreach (Part part in parts.Values)
                if (part.Root) { part.Root.SetActive(false); UnityEngine.Object.Destroy(part.Root); }
            parts.Clear(); owners.Clear();
        }

        internal bool TryContacts(Part receiver, float groundY, List<Contact> contacts,
            out bool foundation, out string reason)
        {
            ThrowIfDisposed();
            if (receiver == null || contacts == null) throw new ArgumentNullException();
            if (!parts.TryGetValue(receiver.Id, out Part owned) || owned != receiver)
                throw new ArgumentException("Receiver belongs to another structural snapshot.", nameof(receiver));
            if (!Finite(groundY)) throw new ArgumentOutOfRangeException(nameof(groundY));
            contacts.Clear(); foundation = false; reason = receiver.UnknownReason;
            receiver.GroundContact = false; receiver.StaticSupport = null;
            if (reason != null || !receiver.Profile.HasValue) { reason = reason ?? "no-structural-profile"; return false; }
            if (rayMask == 0 || hits.Length <= 1) { reason = "native-query-policy-unavailable"; return false; }
            var seen = new HashSet<Collider>();
            bool capacityFailed = false;
            foreach (Collider collider in receiver.Colliders)
            {
                if (!TryEnvelope(collider, out Vector3 center, out Vector3 half, out Quaternion rotation))
                { reason = "unknown-native-envelope"; continue; }
                float reachY = Mathf.Abs((rotation * Vector3.right).y) * half.x +
                    Mathf.Abs((rotation * Vector3.up).y) * half.y + Mathf.Abs((rotation * Vector3.forward).y) * half.z;
                bool groundContact = center.y - reachY <= groundY && center.y + reachY >= groundY;
                if (groundContact) { foundation = true; receiver.GroundContact = true; }
                int count = scene.GetPhysicsScene().OverlapBox(center, half, hits, rotation, rayMask, QueryTriggerInteraction.Ignore);
                // The assumed terrain may itself occupy a native query slot.
                if (count >= hits.Length - (groundContact ? 1 : 0)) { capacityFailed = true; continue; }
                for (int index = 0; index < count; ++index)
                {
                    Collider other = hits[index];
                    if (!seen.Add(other) || !owners.TryGetValue(other, out Part source) || source == receiver) continue;
                    if (source.UnknownReason != null) { reason = "unknown-contact-part"; continue; }
                    if (source.StaticAnchor) { foundation = true; receiver.StaticSupport = source; continue; }
                    if (!source.Relays) continue;
                    if (!source.Profile.HasValue) { reason = "unknown-contact-profile"; continue; }
                    Vector3 point;
                    if (other is MeshCollider mesh && !mesh.convex)
                        point = mesh.Raycast(new Ray(receiver.Com, Vector3.down), out RaycastHit hit, 10f)
                            ? hit.point : (receiver.Com + source.Com) * .5f;
                    else point = Physics.ClosestPoint(receiver.Com, other, other.transform.position, other.transform.rotation);
                    if (!Finite(point)) { reason = "invalid-contact-point"; continue; }
                    contacts.Add(new Contact(source, point));
                }
            }
            if (capacityFailed)
            {
                foundation = false; receiver.GroundContact = false; receiver.StaticSupport = null;
                reason = "native-query-capacity"; contacts.Clear(); return false;
            }
            // A proved maximum-support contact makes other candidate contributions irrelevant.
            if (foundation) { reason = null; return true; }
            return reason == null;
        }

        private static void CaptureProfile(GameObject source, Part part)
        {
            WearNTear native = source.GetComponent<WearNTear>();
            WearNTear[] all = source.GetComponentsInChildren<WearNTear>(true);
            if (all.Length > 1 || all.Length == 1 && !native) { part.UnknownReason = "compound-structural-profile"; return; }
            if (!native) { part.StaticAnchor = true; return; }
            if (MaterialProperties == null || MaterialProperties.ReturnType != typeof(void) ||
                ComOffset?.FieldType != typeof(Vector3) || ForceCom?.FieldType != typeof(bool) || SupportWear?.FieldType != typeof(bool))
            { part.UnknownReason = "native-profile-api-unavailable"; return; }
            try
            {
                var values = new object[] { 0f, 0f, 0f, 0f };
                MaterialProperties.Invoke(native, values);
                part.Profile = new Evaluation.Profile((float)values[0], (float)values[1], (float)values[2], (float)values[3]);
                part.ComOffset = (Vector3)ComOffset.GetValue(native);
                if (!Finite(part.ComOffset) || !Finite(part.Com)) { part.UnknownReason = "invalid-native-com"; return; }
                part.ForceCorrectCom = (bool)ForceCom.GetValue(native);
                part.NoSupportWear = (bool)SupportWear.GetValue(native);
                part.Relays = native.m_supports;
            }
            catch (Exception error) when (error is ArgumentException || error is TargetInvocationException || error is MemberAccessException)
            { part.UnknownReason = "native-profile-read-failed"; }
        }

        private static bool TryEnvelope(Collider collider, out Vector3 center, out Vector3 half, out Quaternion rotation)
        {
            if (collider is BoxCollider box)
            {
                center = box.transform.TransformPoint(box.center); rotation = box.transform.rotation;
                half = (Vector3.Scale(box.transform.lossyScale, box.size) + Vector3.one * .3f) * .5f;
            }
            else
            {
                center = collider.bounds.center; rotation = Quaternion.identity;
                if (!collider.enabled || !collider.gameObject.activeInHierarchy)
                { half = Vector3.zero; return false; }
                half = (collider.bounds.size + Vector3.one * .3f) * .5f;
            }
            return Finite(center) && Finite(half) && half.x > 0 && half.y > 0 && half.z > 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
            parts.Clear(); owners.Clear();
            if (scene.IsValid()) Unloading = SceneManager.UnloadSceneAsync(scene);
        }

        private void ThrowIfDisposed()
        { if (disposed) throw new ObjectDisposedException(nameof(BlueprintStructuralSnapshot)); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}

// Inert Unity hierarchy only. Production helpers, geometry and Harmony patches are linked unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace UnityEngine
{
    public class Object
    {
        public static bool SuppressOnDestroy;
        public bool Alive = true;
        public static implicit operator bool(Object value) => value != null && value.Alive;
        public static void Destroy(Object value) => DestroyImmediate(value);
        public static void DestroyImmediate(Object value)
        {
            if (!value) return;
            if (value is GameObject go)
            {
                foreach (var child in go.transform.Children.ToArray()) DestroyImmediate(child.gameObject);
                foreach (var component in go.Components.ToArray()) DestroyImmediate(component);
                go.transform.SetParent(null, false);
                go.transform.Alive = false;
            }
            else if (value is Component component)
            {
                if(!SuppressOnDestroy) component.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
                component.gameObject.Components.Remove(component);
            }
            value.Alive = false;
        }
    }
    [Flags] public enum HideFlags { HideInHierarchy=1, DontSave=2 }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField:Attribute { }
    public static class Resources
    {
        internal static readonly List<WeakReference<Component>> Loaded=new List<WeakReference<Component>>();
        public static T[] FindObjectsOfTypeAll<T>() where T:Component => Loaded.Select(r=>r.TryGetTarget(out var c)?c:null).OfType<T>().Where(c=>c).ToArray();
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public string name => gameObject.name;
        public string tag { get => gameObject.tag; set => gameObject.tag=value; }
        public bool CompareTag(string value) => gameObject.tag == value;
        public T GetComponent<T>() where T:Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component { }
    public class GameObject : Object
    {
        public static int Writes;
        public string name, tag="Untagged"; public bool activeSelf=true; public HideFlags hideFlags;
        public readonly Transform transform; internal readonly List<Component> Components = new List<Component>();
        public GameObject(string name="") { this.name=name; transform=new Transform { gameObject=this }; }
        public void SetActive(bool value) { ++Writes; activeSelf=value; }
        public T AddComponent<T>() where T:Component => (T)AddComponent(typeof(T));
        private Component AddComponent(Type type) { var c=(Component)Activator.CreateInstance(type,true); c.gameObject=this;Components.Add(c);Resources.Loaded.Add(new WeakReference<Component>(c));return c; }
        public T GetComponent<T>() where T:Component { foreach(var c in Components) if(c is T t && t) return t;return null; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T:Component => Components.OfType<T>().Concat(transform.Children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public class Transform : Component
    {
        internal readonly List<Transform> Children=new List<Transform>(); public Transform parent;
        private Vector3 local; private Quaternion rotation=Quaternion.identity;
        public Vector3 localPosition { get=>local;set { ++GameObject.Writes;local=value; } }
        public Quaternion localRotation { get=>rotation;set { ++GameObject.Writes;rotation=value; } }
        public Vector3 position {get;set;}
        public int childCount=>Children.Count;
        public Transform GetChild(int index)=>Children[index];
        public void SetParent(Transform value,bool worldPositionStays) { parent?.Children.Remove(this);parent=value;value?.Children.Add(this); }
        public void SetSiblingIndex(int index) { parent.Children.Remove(this);parent.Children.Insert(index,this); }
    }
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x,y,z; public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>default;
        public static Vector3 right=>new Vector3(1,0,0);
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
        public bool Equals(Vector3 other)=>x==other.x&&y==other.y&&z==other.z;
    }
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x,y,z,w; public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}
        public static Quaternion identity=>new Quaternion(0,0,0,1);
        public bool Equals(Quaternion other)=>x==other.x&&y==other.y&&z==other.z&&w==other.w;
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)
        { var q=System.Numerics.Quaternion.Slerp(new System.Numerics.Quaternion(a.x,a.y,a.z,a.w),new System.Numerics.Quaternion(b.x,b.y,b.z,b.w),t);return new Quaternion(q.X,q.Y,q.Z,q.W); }
    }
}
public class ZDO { private byte[] bytes; public byte[] GetByteArray(int key,byte[] fallback)=>bytes??fallback; public void Set(int key,byte[] value){bytes=value;} }
public class ZNetView:UnityEngine.MonoBehaviour { private readonly ZDO zdo=new ZDO();public bool IsValid()=>true;public bool IsOwner()=>true;public ZDO GetZDO()=>zdo; }
public static class StableHash {public static int GetStableHashCode(this string value)=>1;}
public class Piece:UnityEngine.MonoBehaviour
{
    public static readonly List<Piece> NearbyPieces=new List<Piece>(); public static Action<Piece> BeforeCollection;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GetSnapPoints(List<UnityEngine.Transform> points)
    { for(int i=0;i<transform.childCount;++i) {var child=transform.GetChild(i);if(child.CompareTag("snappoint"))points.Add(child);} }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void GetSnapPoints(UnityEngine.Vector3 p,float radius,List<UnityEngine.Transform> points,List<Piece> pieces)
    {foreach(var piece in NearbyPieces){BeforeCollection?.Invoke(piece);piece.GetSnapPoints(points);pieces.Add(piece);} }
}
namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksPlugin {internal const string PluginGuid="ostrmod.buildworks";}
    internal static class HammerBlueprintPieceRegistry {internal const string PrefabPrefix="BuildWorks_Blueprint_";}
}

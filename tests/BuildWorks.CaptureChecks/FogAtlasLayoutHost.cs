using System;
using System.Collections.Generic;

// CPU host only. Texture creation is inert; actual dispatch/presentation are tripwires.
namespace UnityEngine
{
    public class Object
    {
        public bool Alive = true;
        public HideFlags hideFlags;
        public static implicit operator bool(Object value) => value != null && value.Alive;
        public static T Instantiate<T>(T value) where T : Object, new() => new T();
        public static void DestroyImmediate(Object value) { if (value != null) value.Alive = false; }
    }
    public enum HideFlags { HideAndDontSave }
    public enum FilterMode { Point, Trilinear }
    public enum TextureWrapMode { Clamp }
    public enum RenderTextureFormat { RHalf, ARGBHalf }
    public enum RenderTextureReadWrite { Linear }
    [Flags] public enum DepthTextureMode { None=0, Depth=1 }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public float magnitude => (float)Math.Sqrt(x*x+y*y+z*z);
        public Vector3 normalized => magnitude == 0 ? this : new Vector3(x/magnitude,y/magnitude,z/magnitude);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    public struct Vector4
    {
        public float x,y,z,w;
        public Vector4(float x,float y,float z,float w) {this.x=x;this.y=y;this.z=z;this.w=w;}
        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x,v.y,v.z,0);
    }
    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x,y,z;
        public Vector3Int(int x,int y,int z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3Int zero => default;
        public static Vector3Int Max(Vector3Int a,Vector3Int b) => new Vector3Int(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Max(a.z,b.z));
        public static Vector3Int operator +(Vector3Int a,Vector3Int b) => new Vector3Int(a.x+b.x,a.y+b.y,a.z+b.z);
        public static bool operator ==(Vector3Int a,Vector3Int b) => a.Equals(b);
        public static bool operator !=(Vector3Int a,Vector3Int b) => !a.Equals(b);
        public bool Equals(Vector3Int b) => x==b.x && y==b.y && z==b.z;
        public override bool Equals(object value) => value is Vector3Int v && Equals(v);
        public override int GetHashCode() => x ^ y<<8 ^ z<<16;
    }
    public struct Bounds
    {
        public Vector3 min,max;
        public Bounds(Vector3 center,Vector3 size) {min=new Vector3(center.x-size.x/2,center.y-size.y/2,center.z-size.z/2);max=new Vector3(center.x+size.x/2,center.y+size.y/2,center.z+size.z/2);}
        public Vector3 size => max-min;
        public void Encapsulate(Bounds b) {min=new Vector3(Math.Min(min.x,b.min.x),Math.Min(min.y,b.min.y),Math.Min(min.z,b.min.z));max=new Vector3(Math.Max(max.x,b.max.x),Math.Max(max.y,b.max.y),Math.Max(max.z,b.max.z));}
    }
    public static class Mathf
    {
        public const float Deg2Rad=(float)Math.PI/180;
        public static int CeilToInt(float v)=>(int)Math.Ceiling(v);
        public static float Floor(float v)=>(float)Math.Floor(v);
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Tan(float v)=>(float)Math.Tan(v);
    }
    public class Texture : Object { public int width=1,height=1; }
    public class Texture3D : Texture
    {
        public int depth=1;
        public byte[] Data=new byte[]{1};
        public T[] GetPixelData<T>(int mip) => typeof(T)==typeof(byte) ? (T[])(object)Data : throw new InvalidOperationException("Byte texture expected.");
    }
    public class RenderTexture : Texture
    {
        public static int Creates;
        public static bool FailNextCreate;
        public static RenderTexture active=null;
        public int volumeDepth=1;
        public bool enableRandomWrite,Released;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Rendering.TextureDimension dimension;
        public RenderTexture() { }
        public RenderTexture(int w,int h,int depth,RenderTextureFormat format,RenderTextureReadWrite mode) {width=w;height=h;}
        public bool Create() {++Creates;bool fail=FailNextCreate;FailNextCreate=false;return !fail;}
        public void Release() {Released=true;}
        public static RenderTexture GetTemporary(int w,int h,int d,RenderTextureFormat f,RenderTextureReadWrite r) => new RenderTexture(w,h,d,f,r);
        public static void ReleaseTemporary(RenderTexture value) => value.Release();
    }
    public class Shader : Object { }
    public class Material : Object
    {
        public Material() { }
        public Material(Shader shader) { }
        public void EnableKeyword(string key) { }
        public void DisableKeyword(string key) { }
        public void SetTexture(string key,Texture value) { }
        public void SetBuffer(string key,ComputeBuffer value) { }
        public void SetVector(string key,Vector4 value) { }
        public void SetVectorArray(string key,Vector4[] value) { }
        public void SetFloat(string key,float value) { }
        public void SetInt(string key,int value) { }
        public int FindPass(string name) => -1;
    }
    public class ComputeBuffer
    {
        public static int Uploads;
        public ComputeBuffer(int count,int stride) { }
        public void SetData(uint[] values) {++Uploads;}
        public void Release() { }
    }
    public class ComputeShader : Object
    {
        public static int Dispatches;
        public int FindKernel(string key)=>0;
        public void SetInts(string key,params int[] values) { }
        public void SetTexture(int kernel,string key,Texture value) { }
        public void SetVector(string key,Vector4 value) { }
        public void Dispatch(int kernel,int x,int y,int z) {++Dispatches;throw new InvalidOperationException("GPU dispatch boundary reached.");}
    }
    public class Transform {public Vector3 position=default,right=default,up=default,forward=default;}
    public class Camera : Object {public bool orthographic=false,stereoEnabled=false;public DepthTextureMode depthTextureMode=DepthTextureMode.Depth;public Transform transform=new Transform();public float aspect=1,fieldOfView=55;}
    public static class SystemInfo {public static int maxTexture3DSize=2048,maxTextureSize=4096;public static bool SupportsRenderTextureFormat(RenderTextureFormat format)=>true;}
    public static class Graphics {public static int Blits;public static void Blit(Texture image,RenderTexture target,Material material=null,int pass=0) {++Blits;throw new InvalidOperationException("GPU presentation boundary reached.");}}
}
namespace UnityEngine.Rendering {public enum TextureDimension {Tex3D}}
namespace OstrixMods.BuildWorks
{
    using UnityEngine;
    internal sealed class BlueprintFogAssets {internal Shader Volume=new Shader();internal ComputeShader Solver=new ComputeShader(),Arena=new ComputeShader();}
    internal sealed class BlueprintFogSource {internal Texture3D Shape=new Texture3D();internal string ShapeHash="shape";internal Texture Noise=new Texture();internal Vector3 Minimum=default,Maximum=default;}
    internal sealed class FakeSimulation {internal int Tick=0;}
    internal sealed class BlueprintFogVisual
    {
        internal BlueprintFogSource Source=new BlueprintFogSource();
        internal RenderTexture Smoke=new RenderTexture(),Surface=new RenderTexture();
        internal Bounds VolumeBounds=new Bounds(default,new Vector3(1,1,1));
        internal Vector3Int FieldSize=new Vector3Int(4,4,4);
        internal Vector3 Origin=default,FieldMinimum=default,FieldMaximum=default;
        internal Vector4 NoiseOffset=default,ActorRadius=default;
        internal float Clock=0,BodyHalfHeight=1;
        internal object LogicalSource=null;
        internal FakeSimulation Simulation=null;
        internal void ValidatePublishedMapping(BlueprintFogChannels pool,BlueprintFogChannels.SnapshotChannel snapshot) { }
        internal void BindPublished(BlueprintFogChannels pool,ComputeBuffer bindings,int index,Vector4 offset) { }
        internal void UnbindPublished() { }
    }
    internal sealed class FakeArena
    {
        internal long RenderBytes=0;
        internal RenderTexture Smoke=new RenderTexture(),Surface=new RenderTexture();
        internal Dictionary<string,ComputeBuffer> Buffers=new Dictionary<string,ComputeBuffer>{{"_PublishedMetadata",new ComputeBuffer(1,4)}};
        internal void BindPublication(Material material) { }
    }
    internal sealed class BlueprintFogChannels
    {
        internal sealed class SnapshotChannel {internal int Slot=0;}
        internal FakeArena SnapshotArena=new FakeArena();
        internal int SnapshotCapacity=32;
        internal void WriteSnapshotBinding(SnapshotChannel snapshot,uint[] words,int offset) { }
    }
}

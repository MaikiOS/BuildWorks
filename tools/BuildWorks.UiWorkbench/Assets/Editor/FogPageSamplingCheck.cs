using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class FogPageSamplingCheck
{
 internal static int Check()
 {
  var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/FogPageSamplingProbe.shader");
  if(!shader || ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))throw new Exception("Page sampler probe shader failed.");
  const int slot=27, bank=1584000, index=bank+505;
  using(var maps=new ComputeBuffer(28*57600,4))
  using(var bases=new ComputeBuffer(28,4))
  using(var metadata=new ComputeBuffer(28*16,4))
  using(var bindings=new ComputeBuffer(5,4))
  using(var shortBuffer=new ComputeBuffer(1,4))
  {
   var material=new Material(shader);var texture=new Texture3D(12,12,6,TextureFormat.RGBAHalf,false){filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
   var target=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);var read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
   try
   {
    var colours=new Color[12*12*6];
    for(int p=1;p<4;++p)for(int z=0;z<6;++z)for(int y=0;y<6;++y)for(int x=0;x<6;++x)
    {bool inside=x>0&&x<5&&y>0&&y<5&&z>0&&z<5;colours[x+(p%2)*6+12*(y+(p/2)*6+12*z)]=p==3?Color.magenta:inside?(p==1?Color.white:new Color(.5f,.25f,1,.75f)):Color.clear;}
    texture.SetPixels(colours);texture.Apply();
    var table=Enumerable.Repeat(uint.MaxValue,28*57600).ToArray();table[index]=1;table[index+19200]=2;maps.SetData(table);
    var pointers=new uint[28];pointers[slot]=bank;bases.SetData(pointers);
    var info=new uint[28*16];info[slot*16+7]=3;info[slot*16+8]=11;info[slot*16+10]=17;metadata.SetData(info);
    var identity=new uint[]{slot,11,0,17,0};bindings.SetData(identity);shortBuffer.SetData(new uint[]{0});
    material.SetBuffer("_PublishedMaps",maps);material.SetBuffer("_PublishedMapBases",bases);material.SetBuffer("_PublishedMetadata",metadata);material.SetBuffer("_FogBindings",bindings);
    material.SetTexture("_PublishedBricks",texture);material.SetVector("_PublishedBrickGrid",new Vector4(2,2,1,0));material.SetInt("_PublishedBlockCapacity",3);material.SetInt("_PublishedSlots",28);
    Vector3 centre=new Vector3(4.5f/96,4.5f/80,4.5f/80);
    Color Sample(int field,Vector3 uv)
    {
     material.SetInt("_ProbeField",field);material.SetVector("_ProbeUV",uv);Graphics.Blit(null,target,material);var old=RenderTexture.active;
     try{RenderTexture.active=target;read.ReadPixels(new Rect(0,0,1,1),0,0);read.Apply();return read.GetPixel(0,0);}finally{RenderTexture.active=old;}
    }
    void Match(Color actual,Color expected,string name)
    {for(int c=0;c<4;++c)if(Math.Abs(actual[c]-expected[c])>.00001f)throw new Exception("Page sampler "+name+" channel"+c+": "+actual+" != "+expected);}
    Match(Sample(0,centre),Color.white,"smoke");Match(Sample(2,centre),new Color(.5f,.25f,1,.75f),"surface");
    Match(Sample(0,new Vector3(3.75f/96,3.75f/80,3.75f/80)),Color.white*.015625f,"empty neighbour corner halo");
    Match(Sample(0,Vector3.zero),Color.clear,"lower edge");Match(Sample(0,Vector3.one),Color.clear,"upper edge");
    int rejected=0;
    void Zero(Action change,Action restore,string name)
    {change();try{Match(Sample(0,centre),Color.clear,name);++rejected;}finally{restore();}}
    void RestoreBases(){pointers[slot]=bank;bases.SetData(pointers);}
    foreach(uint wrong in new uint[]{0,uint.MaxValue,bank+1})Zero(()=>{pointers[slot]=wrong;bases.SetData(pointers);},RestoreBases,"map base");
    foreach(uint wrong in new uint[]{uint.MaxValue,3,uint.MaxValue-1})Zero(()=>{table[index]=wrong;maps.SetData(table);},()=>{table[index]=1;maps.SetData(table);},"page ID");
    foreach(string name in new[]{"_PublishedMaps","_PublishedMapBases","_PublishedMetadata","_FogBindings"})
    {var original=name=="_PublishedMaps"?maps:name=="_PublishedMapBases"?bases:name=="_PublishedMetadata"?metadata:bindings;Zero(()=>material.SetBuffer(name,shortBuffer),()=>material.SetBuffer(name,original),name+" extent");}
    foreach(int word in new[]{1,3})Zero(()=>{identity[word]++;bindings.SetData(identity);},()=>{identity[word]--;bindings.SetData(identity);},"identity");
    Zero(()=>{info[slot*16+7]=0;metadata.SetData(info);},()=>{info[slot*16+7]=3;metadata.SetData(info);},"inactive");
    Match(Sample(1,centre),Color.clear,"unpublished origin field");++rejected;
    Match(Sample(3,centre),Color.clear,"unknown field");++rejected;
    if(rejected!=15)throw new Exception("Page sampling coverage incomplete.");
    return rejected;
   }
   finally{UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(read);target.Release();UnityEngine.Object.DestroyImmediate(target);}
  }
 }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeAtlasCheck
{
    private static readonly Vector3 Beam=new Vector3(-4,1,0);
    public static void Run()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")), output=Path.Combine(root,"artifacts/fog-runtime-atlas");
        Directory.CreateDirectory(output); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        ColorSpace previous=PlayerSettings.colorSpace;
        var host=new GameObject("Atlas camera"); var camera=host.AddComponent<Camera>(); var probe=host.AddComponent<FogRenderProbe>();
        camera.transform.position=new Vector3(3,2.2f,-4); camera.transform.LookAt(Vector3.up); camera.fieldOfView=49;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.2f,.3f);
        camera.allowHDR=true;camera.renderingPath=RenderingPath.DeferredShading;camera.depthTextureMode=DepthTextureMode.Depth;
        var prefabs=new GameObject[3];var materials=new Material[3];var owners=new BlueprintFogVisual[3];
        Vector3[] scales={new Vector3(.2f,2,2),new Vector3(2,.2f,2),new Vector3(.2f,.2f,2)};
        Vector3[] positions={Vector3.up,Vector3.zero,new Vector3(0,1.3f,0)};
        Vector3[] origins={Vector3.zero,new Vector3(.35f,.1f,.2f),new Vector3(-.2f,.05f,-.3f)};
        Vector2[] bodies={new Vector2(.25f,.85f),new Vector2(.3f,.95f),new Vector2(.35f,1.05f)};
        Vector3 start=new Vector3(-1.4f,.85f,0);Color[] nearContact=null;
        long houseLayoutBytes=CheckLayouts();
        int frames=0;long bytes=0;double order=0,pairDelta=0,removalDelta=0,missingOwnerDelta=0,edgeDelta=0,occlusionDelta=0,translatedDelta=0;bool layoutRolled=false;
        try
        {
            PlayerSettings.colorSpace=ColorSpace.Linear;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(QualitySettings.activeColorSpace==ColorSpace.Linear,"Atlas must render in Linear");
            var offsets=new Vector3Int[32];var thin=Enumerable.Repeat(new Vector3Int(10,10,42),32).ToArray();
            Vector3Int layout=BlueprintFogAtlas.Layout(thin,64,offsets);
            Require(layout.x<=64&&layout.y<=64&&layout.z<=64&&offsets.Any(v=>v.x>0)&&offsets.Any(v=>v.y>0),"Atlas failed to roll Z into X/Y");
            for(int i=0;i<32;++i) for(int j=0;j<i;++j)
                Require(!Overlap(offsets[i],thin[i],offsets[j],thin[j]),"Atlas blocks overlap");
            Reject(()=>BlueprintFogAtlas.Layout(new[]{new Vector3Int(65,3,3)},64,offsets));
            Reject(()=>BlueprintFogAtlas.Layout(new[]{new Vector3Int(256,256,256)},2048,offsets));layoutRolled=true;
            using(var assets=new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root,"src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using(var pool=new BlueprintFogChannels(8,BlueprintFogChannels.MaximumFieldBudget))
            using(var atlas=new BlueprintFogAtlas(assets))
            using(var volume=new BlueprintFogVolume(assets))
            {
                for(int i=0;i<3;++i)
                {
                    prefabs[i]=GameObject.CreatePrimitive(PrimitiveType.Cube);prefabs[i].transform.localScale=scales[i];prefabs[i].SetActive(false);
                    materials[i]=new Material(Shader.Find("Standard"));materials[i].color=i==0?Color.red:i==1?Color.green:Color.blue;
                    prefabs[i].GetComponent<Renderer>().sharedMaterial=materials[i];
                    owners[i]=new BlueprintFogVisual(assets,prefabs[i],positions[i],i==2?Quaternion.Euler(0,90,0):Quaternion.identity,
                        i==2?new Vector3(1.2f,1,.8f):Vector3.one,null,origins[i],start,bodies[i],pool,"atlas-"+i,true);
                    for(int warm=1;warm<=i*30;++warm)owners[i].AdvanceTo(warm*BlueprintFogSimulation.Step,start,bodies[i],false);
                }
                var pair=new[]{owners[0],owners[1]};var reverse=new[]{owners[2],owners[1],owners[0]};
                for(int tick=0;tick<=420;++tick)
                {
                    Vector3 actor=tick<=90?Vector3.Lerp(start,new Vector3(0,.85f,0),Mathf.Min(1,tick/30f)):new Vector3(3,.85f,3);
                    for(int i=0;i<3;++i)owners[i].AdvanceTo((tick+i*30)*BlueprintFogSimulation.Step,actor,bodies[i],tick<=90);
                    if(tick!=0&&tick!=45&&tick!=90&&tick!=420) continue;
                    owners[2].Root.SetActive(false);
                    Color[] direct=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.Render(camera,image,target,pair[0],pair[1],Beam,Vector3.right,2),output,tick+"-pair-direct");
                    Color[] packed=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>atlas.Render(camera,image,target,pair,Beam,Vector3.right,2),output,tick+"-pair-atlas");
                    pairDelta=Math.Max(pairDelta,FogRuntimePairCheck.Delta(direct,packed));owners[2].Root.SetActive(true);
                    Color[] abc=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.RenderMany(camera,image,target,owners,Beam,Vector3.right,2),output,tick+"-ABC");
                    Color[] cba=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.RenderMany(camera,image,target,reverse,Beam,Vector3.right,2),output,tick+"-CBA");
                    order=Math.Max(order,FogRuntimePairCheck.Delta(abc,cba));bytes=Math.Max(bytes,volume.AtlasBytes);++frames;
                    if(tick==90)
                    {
                        nearContact=abc;
                        float[][] before={Read(owners[0].Simulation.Smoke,4),Read(owners[1].Simulation.Surface,4),Read(owners[2].Simulation.Smoke,4)};
                        Reject(()=>atlas.Render(camera,null,null,new[]{owners[0],owners[0],owners[2]},Beam,Vector3.right,2));
                        using(var far=new BlueprintFogVisual(assets,prefabs[0],new Vector3(100,1,0),Quaternion.identity,Vector3.one,null,new Vector3(100,0,0),start,bodies[0],pool,"far",true))
                            Reject(()=>atlas.Render(camera,null,null,new[]{owners[0],owners[1],far},Beam,Vector3.right,2));
                        Require(before[0].SequenceEqual(Read(owners[0].Simulation.Smoke,4))&&before[1].SequenceEqual(Read(owners[1].Simulation.Surface,4))&&
                            before[2].SequenceEqual(Read(owners[2].Simulation.Smoke,4)),"Render/preflight failure changed simulation owners");
                        var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);
                        try
                        {
                            occluder.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward,camera.transform.rotation);
                            occluder.transform.localScale=new Vector3(12,12,.1f);occluder.GetComponent<Renderer>().sharedMaterial=materials[0];
                            Color[] plain=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>Graphics.Blit(image,target),output,"opaque-reference");
                            Color[] hidden=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.RenderMany(camera,image,target,owners,Beam,Vector3.right,2),output,"opaque-atlas");
                            occlusionDelta=FogRuntimePairCheck.Delta(plain,hidden);Require(occlusionDelta==0,"Atlas drew through opaque depth");
                        }
                        finally{UnityEngine.Object.DestroyImmediate(occluder);}
                    }
                }
                Require(order<.000001&&pairDelta<.00001&&bytes>0&&bytes<=BlueprintFogAtlas.MaximumBytes,"Atlas order/pair/budget gate failed");
                CheckPackingCache(assets,camera,probe,owners,prefabs,positions,origins,bodies,start,output);
                owners[2].Root.SetActive(false);
                Color[] oldPair=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.Render(camera,image,target,pair[0],pair[1],Beam,Vector3.right,2),output,"removal-direct");
                Color[] remaining=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.RenderMany(camera,image,target,pair,Beam,Vector3.right,2),output,"removal-wrapper");
                removalDelta=FogRuntimePairCheck.Delta(oldPair,remaining);
                Require(removalDelta==0&&volume.AtlasBytes==0,"Removing third owner did not release derived atlas/exact pair fallback");
                for(int i=0;i<3;++i) Fill(owners[i].Simulation.Smoke,i==0?new Color(.8f,.1f,.2f,1):i==1?new Color(.1f,.7f,.3f,1):new Color(.2f,.3f,.9f,1));
                // This fixture writes GPU fields outside the simulation clock; invalidate its private render cache.
                ResetPacking(atlas);
                Color[] full=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>atlas.Render(camera,image,target,owners,Beam,Vector3.right,0),output,"solid-three");
                CheckPacked(atlas,owners);edgeDelta=CheckEdges(atlas,owners);
                var oldOffsets=Field<Vector3Int[]>(atlas,"offsets").ToArray();
                var oldBlocks=Field<List<Vector3Int>>(atlas,"layoutBlocks").ToArray();
                var oldShape=Field<RenderTexture>(atlas,"shape");bool allocationCalled=false;
                Reject(()=>atlas.ChangeLayout(new[]{new Vector3Int(20,30,40),new Vector3Int(50,20,15)},2048,size=>{
                    allocationCalled=true;throw new InvalidOperationException("Injected allocation failure");}));
                Require(allocationCalled&&oldOffsets.SequenceEqual(Field<Vector3Int[]>(atlas,"offsets"))&&
                    oldBlocks.SequenceEqual(Field<List<Vector3Int>>(atlas,"layoutBlocks"))&&oldShape==Field<RenderTexture>(atlas,"shape"),
                    "Failed resize committed coordinates without its textures");
                Color[] restored=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>atlas.Render(camera,image,target,owners,Beam,Vector3.right,0),output,"allocation-failed-restore");
                Require(FogRuntimePairCheck.Delta(full,restored)==0,"Previous layout changed after failed allocation");
                Color[] omitted=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>atlas.Render(camera,image,target,pair,Beam,Vector3.right,0),output,"solid-two");
                missingOwnerDelta=FogRuntimePairCheck.Delta(full,omitted);
                Require(missingOwnerDelta>.001&&edgeDelta<.00001,"Third density absent or filtering crossed owners");
                atlas.Dispose();Require(atlas.Bytes==0,"Atlas survived Dispose");
                foreach(var owner in owners)owner.Dispose();Require(pool.Count==0&&pool.FieldBytes==0,"Atlas cleanup removed/retained wrong fields");
                Vector3 shift=new Vector3(2500,45,-1900);camera.transform.position+=shift;
                for(int i=0;i<3;++i)
                {
                    owners[i]=new BlueprintFogVisual(assets,prefabs[i],shift+positions[i],i==2?Quaternion.Euler(0,90,0):Quaternion.identity,
                        i==2?new Vector3(1.2f,1,.8f):Vector3.one,null,shift+origins[i],shift+start,bodies[i],pool,"shift-"+i,true);
                    for(int warm=1;warm<=i*30;++warm)owners[i].AdvanceTo(warm*BlueprintFogSimulation.Step,shift+start,bodies[i],false);
                }
                for(int tick=0;tick<=90;++tick)for(int i=0;i<3;++i)
                    owners[i].AdvanceTo((tick+i*30)*BlueprintFogSimulation.Step,shift+Vector3.Lerp(start,new Vector3(0,.85f,0),Mathf.Min(1,tick/30f)),bodies[i],true);
                Color[] translated=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.RenderMany(camera,image,target,owners,shift+Beam,Vector3.right,2),output,"90-translated");
                translatedDelta=FogRuntimePairCheck.Delta(nearContact,translated);Require(translatedDelta<.005,"World translation broke multi-owner atlas");
                volume.Dispose();Require(volume.AtlasBytes==0&&owners.All(owner=>owner.Simulation.Smoke),"Volume disposal destroyed borrowed simulations");
                foreach(var owner in owners)owner.Dispose();Require(pool.Count==0&&pool.FieldBytes==0,"Translated atlas cleanup failed");
            }
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonUtility.ToJson(new Report{
                passed=true,frames=frames,owners=3,atlasBytes=bytes,houseLayoutBytes=houseLayoutBytes,maxOrderDelta=order,pairDelta=pairDelta,removalDelta=removalDelta,
                missingOwnerDelta=missingOwnerDelta,edgeDelta=edgeDelta,occlusionDelta=occlusionDelta,translatedDelta=translatedDelta,layoutRolled=layoutRolled,packedFieldsExact=true,
                guardsRetainSimulations=true,allocationFailureRecovered=true,packingCache=true,cleanup=true,gameHostTested=false,wholeHouseSupported=false},true));
        }
        finally
        {
            foreach(var owner in owners)owner?.Dispose();probe.Render=null;
            foreach(var prefab in prefabs)if(prefab)UnityEngine.Object.DestroyImmediate(prefab);
            foreach(var material in materials)if(material)UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(host);PlayerSettings.colorSpace=previous;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
    internal static void ResetPacking(BlueprintFogAtlas atlas)
    {
        var cached=typeof(BlueprintFogAtlas).GetField("packedOwners",BindingFlags.NonPublic|BindingFlags.Instance);
        Require(cached!=null,"Packing cache fixture is not connected to production code");
        var array=(Array)cached.GetValue(atlas);Array.Clear(array,0,array.Length);
    }
    private static void CheckPackingCache(BlueprintFogAssets assets,Camera camera,FogRenderProbe probe,BlueprintFogVisual[] owners,
        GameObject[] prefabs,Vector3[] positions,Vector3[] origins,Vector2[] bodies,Vector3 start,string output)
    {
        using(var atlas=new BlueprintFogAtlas(assets))using(var reference=new BlueprintFogAtlas(assets))
        {
            Color[] Render(BlueprintFogAtlas a,BlueprintFogVisual[] set,string name)=>FogRuntimePairCheck.Capture(camera,probe,
                (image,target)=>a.Render(camera,image,target,set,Beam,Vector3.right,0),output,name);
            void Equal(BlueprintFogVisual[] set,string name)
            {
                Color[] cached=Render(atlas,set,name+"-cached");ResetPacking(reference);
                Require(FogRuntimePairCheck.Delta(cached,Render(reference,set,name+"-forced"))==0,"Cached atlas pixels differ: "+name);
            }
            Equal(owners,"cache-initial");long before=atlas.PackDispatches;
            Equal(owners,"cache-repeat");Require(atlas.PackDispatches==before,"Unchanged owners were packed again");
            owners[0].AdvanceTo(owners[0].Clock+BlueprintFogSimulation.Step,start,bodies[0],false);
            Equal(owners,"cache-one-tick");Require(atlas.PackDispatches==before+1,"One changed owner did not repack exactly once");
            var reverse=new[]{owners[2],owners[1],owners[0]};before=atlas.PackDispatches;
            Equal(reverse,"cache-reorder");Require(atlas.PackDispatches==before+3,"Changed block layout retained stale packed owners");
            using(var replacement=new BlueprintFogVisual(assets,prefabs[2],positions[2],Quaternion.Euler(0,90,0),
                new Vector3(1.2f,1,.8f),null,origins[2],start,bodies[2],null,null,true))
            {
                var replaced=new[]{replacement,owners[1],owners[0]};before=atlas.PackDispatches;
                Equal(replaced,"cache-owner-replaced");Require(atlas.PackDispatches==before+1,"Same-size replacement reused previous owner's data");
                before=atlas.PackDispatches;
                Reject(()=>atlas.ChangeLayout(new[]{new Vector3Int(20,30,40)},2048,size=>{throw new InvalidOperationException("Injected cache allocation failure");}));
                Equal(replaced,"cache-failed-layout");Require(atlas.PackDispatches==before,"Failed layout discarded valid cache");
                atlas.ChangeLayout(Field<List<Vector3Int>>(atlas,"layoutBlocks").ToArray(),2048,size=>{});
                Equal(replaced,"cache-layout-rebuilt");Require(atlas.PackDispatches==before+3,"Committed layout retained stale packed blocks");
            }
        }
    }
    private static long CheckLayouts()
    {
        // Sanitized saved-bounds dimensions, including filtering borders; no game assets or personal IDs.
        var house = new List<Vector3Int>();
        foreach (var entry in new[]{new Vector4(42,26,79,6),new Vector4(69,26,80,4),new Vector4(50,59,79,4),
            new Vector4(68,59,42,4),new Vector4(97,49,87,4),new Vector4(76,46,42,4),new Vector4(84,49,42,2)})
            for(int i=0;i<(int)entry.w;++i)house.Add(new Vector3Int((int)entry.x,(int)entry.y,(int)entry.z));
        long bytes=CheckLayout(house,2048);
        Require(bytes<=BlueprintFogAtlas.MaximumBytes,"Saved 28-part house still exceeds atlas budget");
        var random=new System.Random(207);
        for(int test=0;test<100;++test)
        {
            var blocks=new List<Vector3Int>();
            for(int i=0;i<32;++i)blocks.Add(new Vector3Int(random.Next(3,25),random.Next(3,25),random.Next(3,25)));
            CheckLayout(blocks,128);
        }
        var offsets=new Vector3Int[32];
        Reject(()=>BlueprintFogAtlas.Layout(null,64,offsets));
        Reject(()=>BlueprintFogAtlas.Layout(new Vector3Int[0],64,offsets));
        Reject(()=>BlueprintFogAtlas.Layout(Enumerable.Repeat(new Vector3Int(3,3,3),33).ToArray(),64,new Vector3Int[33]));
        Reject(()=>BlueprintFogAtlas.Layout(new[]{new Vector3Int(3,3,3)},0,offsets));
        Reject(()=>BlueprintFogAtlas.Layout(new[]{new Vector3Int(3,2,3)},64,offsets));
        Reject(()=>BlueprintFogAtlas.Layout(new[]{new Vector3Int(int.MaxValue,int.MaxValue,int.MaxValue)},int.MaxValue,offsets));
        Reject(()=>BlueprintFogAtlas.Layout(Enumerable.Repeat(new Vector3Int(128,128,128),4).ToArray(),2048,offsets));
        return bytes;
    }
    private static long CheckLayout(IReadOnlyList<Vector3Int> blocks,int limit)
    {
        var offsets=new Vector3Int[blocks.Count];var repeated=new Vector3Int[blocks.Count];
        Vector3Int size=BlueprintFogAtlas.Layout(blocks,limit,offsets);
        Require(size==BlueprintFogAtlas.Layout(blocks,limit,repeated)&&offsets.SequenceEqual(repeated),"Atlas layout is not deterministic");
        Require(size.x<=limit&&size.y<=limit&&size.z<=limit,"Atlas exceeds device bounds");
        long bytes=(long)size.x*size.y*size.z*18;Require(bytes<=BlueprintFogAtlas.MaximumBytes,"Atlas exceeds memory bounds");
        for(int i=0;i<blocks.Count;++i)
        {
            Vector3Int end=offsets[i]+blocks[i];
            Require(offsets[i].x>=0&&offsets[i].y>=0&&offsets[i].z>=0&&end.x<=size.x&&end.y<=size.y&&end.z<=size.z,"Atlas clips a block");
            for(int j=0;j<i;++j)Require(!Overlap(offsets[i],blocks[i],offsets[j],blocks[j]),"Atlas overlaps filtering borders");
        }
        return bytes;
    }
    private static T Field<T>(BlueprintFogAtlas atlas,string name)=>(T)typeof(BlueprintFogAtlas).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(atlas);
    private static void CheckPacked(BlueprintFogAtlas atlas,BlueprintFogVisual[] owners)
    {
        RenderTexture shape=Field<RenderTexture>(atlas,"shape"),smoke=Field<RenderTexture>(atlas,"smoke"),surface=Field<RenderTexture>(atlas,"surface");
        Vector3Int[] offsets=Field<Vector3Int[]>(atlas,"offsets");
        float[] a=Read(shape,1),b=Read(smoke,4),c=Read(surface,4);
        for(int i=0;i<owners.Length;++i)
        {
            var source=owners[i].Source.Shape;var data=source.GetPixelData<byte>(0);Vector3Int offset=offsets[i];
            for(int z=0;z<source.depth;++z)for(int y=0;y<source.height;++y)for(int x=0;x<source.width;++x)
            {
                int target=offset.x+x+1+shape.width*(offset.y+y+1+shape.height*(offset.z+z+1));
                Require(Math.Abs(a[target]-data[x+source.width*(y+source.height*z)]/255f)<.0005f,"Packed source precision/offset failed");
            }
            RenderTexture field=owners[i].Simulation.Smoke;float[] originalSmoke=Read(field,4),originalSurface=Read(owners[i].Simulation.Surface,4);
            for(int z=0;z<field.volumeDepth;++z)for(int y=0;y<field.height;++y)for(int x=0;x<field.width;++x)for(int channel=0;channel<4;++channel)
            {
                int original=4*(x+field.width*(y+field.height*z))+channel;
                int target=4*(offset.x+x+1+smoke.width*(offset.y+y+1+smoke.height*(offset.z+z+1)))+channel;
                Require(b[target]==originalSmoke[original]&&c[target]==originalSurface[original],"Packing mixed independent transport/return fields");
            }
        }
    }
    private static double CheckEdges(BlueprintFogAtlas atlas,BlueprintFogVisual[] owners)
    {
        RenderTexture packed=Field<RenderTexture>(atlas,"smoke");Vector3Int[] offsets=Field<Vector3Int[]>(atlas,"offsets");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/FogAtlasSamplingCheck.shader"));
        var target=RenderTexture.GetTemporary(8,8,0,RenderTextureFormat.ARGBHalf);double maximum=0;
        try
        {
            material.SetTexture("_Packed",packed);material.SetVector("_PackedSize",new Vector4(packed.width,packed.height,packed.volumeDepth,0));
            for(int i=0;i<owners.Length;++i)foreach(float z in new[]{-.05f,.37f,1.05f})
            {
                RenderTexture original=owners[i].Simulation.Smoke;Vector3Int offset=offsets[i];
                material.SetTexture("_Original",original);material.SetVector("_OriginalSize",new Vector4(original.width,original.height,original.volumeDepth,0));
                material.SetVector("_Offset",new Vector4(offset.x,offset.y,offset.z,0));material.SetFloat("_Slice",z);
                Graphics.Blit(null,target,material);var request=AsyncGPUReadback.Request(target,0,TextureFormat.RGBAFloat);request.WaitForCompletion();
                Require(!request.hasError,"Atlas border sampler readback failed");foreach(float value in request.GetData<float>()){Require(!float.IsNaN(value)&&!float.IsInfinity(value),"Nonfinite atlas border");maximum=Math.Max(maximum,Math.Abs(value));}
            }
        }
        finally{RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(material);}return maximum;
    }
    private static void Fill(RenderTexture field,Color color)
    {
        var texture=new Texture3D(field.width,field.height,field.volumeDepth,TextureFormat.RGBAHalf,false);
        try{texture.SetPixels(Enumerable.Repeat(color,field.width*field.height*field.volumeDepth).ToArray());texture.Apply();Graphics.CopyTexture(texture,field);}
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    private static float[] Read(RenderTexture field,int components)
    {
        var request=AsyncGPUReadback.Request(field,0,components==1?TextureFormat.RFloat:TextureFormat.RGBAFloat);request.WaitForCompletion();Require(!request.hasError,"Atlas readback failed");
        var result=new float[field.width*field.height*field.volumeDepth*components];int offset=0;
        for(int layer=0;layer<request.layerCount;++layer){var data=request.GetData<float>(layer);for(int i=0;i<data.Length;++i)result[offset+i]=data[i];offset+=data.Length;}
        Require(offset==result.Length&&result.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)),"Incomplete/nonfinite atlas readback");return result;
    }
    private static bool Overlap(Vector3Int a,Vector3Int sa,Vector3Int b,Vector3Int sb)=>a.x<b.x+sb.x&&b.x<a.x+sa.x&&a.y<b.y+sb.y&&b.y<a.y+sa.y&&a.z<b.z+sb.z&&b.z<a.z+sa.z;
    private static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(InvalidOperationException){rejected=true;}Require(rejected,"Invalid atlas input accepted");}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    [Serializable]private sealed class Report
    {
        public bool passed,layoutRolled,packedFieldsExact,guardsRetainSimulations,allocationFailureRecovered,packingCache,cleanup,gameHostTested,wholeHouseSupported;
        public int frames,owners;public long atlasBytes,houseLayoutBytes;public double maxOrderDelta,pairDelta,removalDelta,missingOwnerDelta,edgeDelta,occlusionDelta,translatedDelta;
    }
}

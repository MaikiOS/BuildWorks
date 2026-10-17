using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeLazyCheck
{
    public static void Run()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")), output=Path.Combine(root,"artifacts/fog-runtime-lazy");
        Directory.CreateDirectory(output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        ColorSpace previous=PlayerSettings.colorSpace;var host=new GameObject("Lazy camera");var camera=host.AddComponent<Camera>();var probe=host.AddComponent<FogRenderProbe>();
        camera.transform.position=new Vector3(3,2.2f,-4);camera.transform.LookAt(Vector3.up);camera.fieldOfView=49;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.2f,.3f);camera.allowHDR=true;
        camera.renderingPath=RenderingPath.DeferredShading;camera.depthTextureMode=DepthTextureMode.Depth;
        var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.transform.localScale=new Vector3(.2f,2,2);prefab.SetActive(false);
        var checker=new Texture2D(8,8,TextureFormat.RGBA32,false);
        checker.SetPixels(Enumerable.Range(0,64).Select(i=>(i/8+i%8)%2==0?Color.white:Color.gray).ToArray());checker.Apply();
        var material=new Material(Shader.Find("Standard"));material.mainTexture=checker;material.color=new Color(.6f,.4f,.2f);prefab.GetComponent<Renderer>().sharedMaterial=material;
        var all=new BlueprintFogVisual[28];BlueprintFogVisual eager=null,lazy=null;int frames=0;long peak=0,restAtlas=0;double idleDelta=0,dynamicDelta=0,foregroundDelta=0;
        try
        {
            PlayerSettings.colorSpace=ColorSpace.Linear;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            using(var assets=new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root,"src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using(var pool=new BlueprintFogChannels(32,BlueprintFogChannels.MaximumFieldBudget))
            using(var volume=new BlueprintFogVolume(assets))
            {
                CheckAdmission(assets,prefab);CheckStaleRecovery(assets,prefab);
                Vector2 body=new Vector2(.3f,.95f);Vector3 start=new Vector3(-3,.85f,0),position=Vector3.up;
                eager=new BlueprintFogVisual(assets,prefab,position,Quaternion.identity,Vector3.one,null,Vector3.zero,start,body,null,null,true);
                lazy=new BlueprintFogVisual(assets,prefab,position,Quaternion.identity,Vector3.one,null,Vector3.zero,start,body,pool,"lazy",true,true);
                Require(lazy.Simulation==null&&pool.FieldBytes==0&&lazy.Smoke==assets.Empty,"Dormant owner allocated fields");
                double Compare(int label)
                {
                    lazy.Root.SetActive(false);eager.Root.SetActive(true);
                    Color[] a=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>eager.RenderVolume(camera,image,target,new Vector3(-4,1,0),Vector3.right,0),output,label+"-eager");
                    eager.Root.SetActive(false);lazy.Root.SetActive(true);
                    Color[] b=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>lazy.RenderVolume(camera,image,target,new Vector3(-4,1,0),Vector3.right,0),output,label+"-lazy");
                    lazy.Root.SetActive(false);
                    Color[] empty=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>Graphics.Blit(image,target),output,label+"-empty");
                    lazy.Root.SetActive(true);
                    double foreground=0;int pixels=0;
                    for(int i=0;i<a.Length;++i)
                        if(Math.Abs(a[i].r-empty[i].r)+Math.Abs(a[i].g-empty[i].g)+Math.Abs(a[i].b-empty[i].b)>.06)
                        {foreground+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);++pixels;}
                    Require(pixels>100,"Lazy comparison has no foreground");foregroundDelta=Math.Max(foregroundDelta,foreground/(pixels*3));++frames;
                    return FogRuntimePairCheck.Delta(a,b);
                }
                for(int tick=1;tick<=300;++tick){float t=tick*BlueprintFogSimulation.Step;eager.AdvanceTo(t,start,body,false);lazy.AdvanceTo(t,start,body,true);}
                idleDelta=Compare(300);Require(idleDelta==0&&pool.Count==0,"Dormant visual differs or idles with fields");
                for(int tick=301;tick<=390;++tick)
                {
                    Vector3 actor=Vector3.Lerp(start,new Vector3(0,.85f,0),Mathf.Min(1,(tick-300)/60f));float t=tick*BlueprintFogSimulation.Step;
                    eager.AdvanceTo(t,actor,body,true);lazy.AdvanceTo(t,actor,body,true);peak=Math.Max(peak,pool.FieldBytes);
                    if(tick==360||tick==390){Require(lazy.Simulation!=null&&MaxAlpha(lazy.Simulation.Surface)>.5f,"Lazy contact absent");dynamicDelta=Math.Max(dynamicDelta,Compare(tick));}
                }
                Require(lazy.Simulation.Tick==eager.Simulation.Tick,"Delayed activation reset absolute clock");
                for(int tick=391;tick<=720;++tick)
                {
                    float t=tick*BlueprintFogSimulation.Step;Vector3 away=new Vector3(3,.85f,3);
                    eager.AdvanceTo(t,away,body,true);lazy.AdvanceTo(t,away,body,true);
                    if(tick%30==0)AsyncGPUReadback.WaitAllRequests();
                }
                lazy.AdvanceTo(721*BlueprintFogSimulation.Step,start,body,false);eager.AdvanceTo(721*BlueprintFogSimulation.Step,start,body,false);
                Require(lazy.Simulation==null&&pool.Count==0&&pool.FieldBytes==0&&assets.Empty,"Recovered owner not released or empty texture destroyed");
                for(int tick=722;tick<=811;++tick)
                {
                    Vector3 actor=Vector3.Lerp(start,new Vector3(0,.85f,0),Mathf.Min(1,(tick-721)/60f));float t=tick*BlueprintFogSimulation.Step;
                    eager.AdvanceTo(t,actor,body,true);lazy.AdvanceTo(t,actor,body,true);
                    if(tick==781||tick==811)dynamicDelta=Math.Max(dynamicDelta,Compare(tick));
                }
                Require(dynamicDelta<.005&&foregroundDelta<.03,"Lazy first/re-entry visual differs from eager v11");
                eager.Dispose();eager=null;lazy.Dispose();lazy=null;Require(pool.Count==0&&assets.Empty,"Visual disposal destroyed shared empty field");
                for(int i=0;i<28;++i)
                {
                    Vector3 p=new Vector3((i%7)*2,1,(i/7)*2);
                    all[i]=new BlueprintFogVisual(assets,prefab,p,Quaternion.identity,Vector3.one,null,p,new Vector3(30,1,30),body,pool,"rest-"+i,true,true);
                }
                Require(pool.Count==0&&pool.FieldBytes==0&&all.All(v=>v.Simulation==null),"Resting 28-part set allocated simulations");
                FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.RenderMany(camera,image,target,all,new Vector3(-4,1,0),Vector3.right,0),output,"28-dormant");
                restAtlas=volume.AtlasBytes;Require(restAtlas>0&&restAtlas<=BlueprintFogAtlas.MaximumBytes,"Dormant owners did not render through atlas");
                var victim=all[0];victim.AdvanceTo(BlueprintFogSimulation.Step,new Vector3(0,1,0),body,true);
                Require(victim.Simulation!=null&&pool.Count==1,"Contact did not activate one owner");
                FogRuntimePairCheck.Capture(camera,probe,(image,target)=>volume.RenderMany(camera,image,target,all,new Vector3(-4,1,0),Vector3.right,0),output,"28-mixed");
                all[1].Dispose();all[1]=null;Require(assets.Empty&&victim.Simulation.Smoke&&pool.Count==1,"Dormant removal destroyed live neighbor");
                foreach(var v in all)v?.Dispose();Require(pool.Count==0&&pool.FieldBytes==0,"Lazy cleanup retained fields");
                var blueprint=new CompositeBlueprintStore.Blueprint();
                for(int i=0;i<28;++i)blueprint.parts.Add(new CompositeBlueprintStore.Part {
                    stableId="part-"+i,prefabName="wall",position=new CompositeBlueprintStore.VectorData(new Vector3((i%7)*2,1,(i/7)*2)),
                    rotation=new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(0,i%2*90,0)),scale=new CompositeBlueprintStore.VectorData(new Vector3(1.1f,.9f,1.2f))});
                Vector3 origin=new Vector3(4,2,3);Quaternion rotation=Quaternion.Euler(0,90,0);
                camera.transform.position=origin+rotation*new Vector3(14,7,-8);camera.transform.LookAt(origin+rotation*new Vector3(6,1,3));
                Color[] emptyBuilding=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>Graphics.Blit(image,target),output,"28-blueprint-empty");
                using(var building=new BlueprintFogBuilding(assets,pool,blueprint,name=>prefab,origin,rotation,origin+Vector3.up,body))
                {
                    Require(building.Count==28&&pool.Count==0,"Blueprint view did not preserve dormant part count");
                    var built=(System.Collections.Generic.List<BlueprintFogVisual>)typeof(BlueprintFogBuilding).GetField("parts",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(building);
                    for(int i=0;i<28;++i)
                    {
                        var part=blueprint.parts[i];var view=built[i].Root.transform;
                        Require(Vector3.Distance(view.position,origin+rotation*part.position.ToVector3())<.00001f&&
                            Quaternion.Angle(view.rotation,rotation*part.rotation.ToQuaternion())<.001f&&
                            Vector3.Distance(view.localScale,part.RootScale(prefab.transform.localScale))<.00001f,"Blueprint view changed root TRS");
                    }
                    building.AdvanceTo(BlueprintFogSimulation.Step,origin+Vector3.up,body,true);
                    Require(pool.Count>0&&pool.FieldBytes<=BlueprintFogChannels.MaximumFieldBudget,"Blueprint contact did not acquire bounded fields");
                    Color[] builtImage=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>building.Render(camera,image,target,new Vector3(-4,1,0),Vector3.right,0),output,"28-blueprint");
                    Require(FogRuntimePairCheck.Delta(emptyBuilding,builtImage)>.001,"Blueprint adapter rendered an empty frame");
                }
                Require(pool.Count==0&&assets.Empty,"Blueprint cleanup destroyed borrowed resources");
            }
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonUtility.ToJson(new Report{passed=true,frames=frames,owners=28,
                idleDelta=idleDelta,maxImageDelta=dynamicDelta,maxForegroundDelta=foregroundDelta,peakFieldBytes=peak,restAtlasBytes=restAtlas,
                delayedClock=true,recoveredReleased=true,reentered=true,sharedEmptyRetained=true,mixedAtlas=true,blueprintAdapter=true,blueprintVisible=true,narrowContact=true,budgetFailureRetainsNeighbor=true,staleRecoveryRetained=true,gameHostTested=false,wholeHouseSupported=false},true));
        }
        finally{eager?.Dispose();lazy?.Dispose();foreach(var v in all)v?.Dispose();UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(prefab);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(checker);PlayerSettings.colorSpace=previous;}
    }
    private static void CheckAdmission(BlueprintFogAssets assets,GameObject prefab)
    {
        Vector2 body=new Vector2(.3f,.95f);Vector3 actor=Vector3.up;
        var sparse=new GameObject("Sparse source");
        try
        {
            foreach(float x in new[]{-1.7f,1.7f})
            {
                var cube=UnityEngine.Object.Instantiate(prefab,sparse.transform);cube.transform.localPosition=new Vector3(x,1,0);cube.SetActive(true);
            }
            sparse.SetActive(false);
            using(var v=new BlueprintFogVisual(assets,sparse,Vector3.zero,Quaternion.identity,Vector3.one,null,Vector3.zero,actor,body,null,null,true))
            {
                Require(!v.Source.MightContact(actor,actor,body),"Empty space within source bounds activates fields");
                Require(v.Source.MightContact(new Vector3(-3,1,0),new Vector3(3,1,0),body),"Swept contact between endpoints was missed");
                Require(v.Source.MightContact(new Vector3(-1.7f,1,0),new Vector3(-1.7f,1,0),body),"Occupied contact was missed");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(sparse);}
        using(var v=new BlueprintFogVisual(assets,prefab,Vector3.up,Quaternion.identity,Vector3.one,null,Vector3.zero,actor,body,null,null,true))
        using(var pool=new BlueprintFogChannels(32,v.Simulation.AllocatedFieldBytes))
        using(var live=new BlueprintFogVisual(assets,prefab,Vector3.up,Quaternion.identity,Vector3.one,null,Vector3.zero,actor,body,pool,"live",true))
        {
            Require(!v.Source.MightContact(new Vector3(-3,1,3),new Vector3(3,1,1),body),"Diagonal bounds without path contact allocated fields");
            foreach(float invalid in new[]{-1f,float.NaN,float.PositiveInfinity,86401f})
                Reject(()=>{using(var rejected=new BlueprintFogSimulation(assets,v.Source.Shape,v.Source.Pigment,v.Source.Noise,
                    v.FieldMinimum,v.FieldMaximum,v.Source.Minimum,v.Source.Maximum,actor,37,v.Simulation.GridSize,invalid)) { }});
            for(int i=1;i<=30;++i)live.AdvanceTo(i*BlueprintFogSimulation.Step,actor,body,true);
            float damage=MaxAlpha(live.Simulation.Surface),smoke=MaxAlpha(live.Simulation.Smoke);Require(damage>.5f&&smoke>0,"Budget negative control has no live matter");
            using(var blocked=new BlueprintFogVisual(assets,prefab,Vector3.up,Quaternion.identity,Vector3.one,null,Vector3.zero,actor,body,pool,"blocked",true,true))
            {
                Reject(()=>blocked.AdvanceTo(BlueprintFogSimulation.Step,actor,body,true));
                Require(blocked.Simulation==null&&pool.Count==1&&MaxAlpha(live.Simulation.Surface)==damage&&MaxAlpha(live.Simulation.Smoke)==smoke,"Budget refusal changed a neighbor");
            }
            using(var duplicate=new BlueprintFogVisual(assets,prefab,Vector3.up,Quaternion.identity,Vector3.one,null,Vector3.zero,actor,body,pool,"live",true,true))
                Reject(()=>duplicate.AdvanceTo(BlueprintFogSimulation.Step,actor,body,true));
            Require(pool.Count==1&&live.Simulation.Smoke,"Duplicate dormant cleanup removed original owner");
        }
    }
    private static void CheckStaleRecovery(BlueprintFogAssets assets,GameObject prefab)
    {
        var actor=Vector3.up;var body=new Vector2(.3f,.95f);
        using(var pool=new BlueprintFogChannels(2,BlueprintFogChannels.MaximumFieldBudget))
        using(var v=new BlueprintFogVisual(assets,prefab,Vector3.up,Quaternion.identity,Vector3.one,null,Vector3.zero,actor,body,pool,"stale",true,true))
        {
            v.AdvanceTo(0,actor,body,true);var channel=v.Simulation;
            Require(channel!=null&&!channel.TryGetRecovered(),"Recovery negative control did not enqueue empty readback");AsyncGPUReadback.WaitAllRequests();
            v.AdvanceTo(BlueprintFogSimulation.Step,actor,body,true);
            v.AdvanceTo(BlueprintFogSimulation.Step*2,new Vector3(8,1,0),body,true);
            v.AdvanceTo(BlueprintFogSimulation.Step*3,new Vector3(8,1,0),body,true);
            Require(v.Simulation==channel&&pool.Count==1&&MaxAlpha(channel.Surface)>0,"Stale empty readback released newly emitted matter");
        }
    }
    private static void Reject(Action action)
    {
        bool failed=false;try{action();}catch(ArgumentException){failed=true;}catch(InvalidOperationException){failed=true;}
        Require(failed,"Invalid lazy operation was accepted");
    }
    private static float MaxAlpha(RenderTexture texture)
    {
        var request=AsyncGPUReadback.Request(texture,0,TextureFormat.RGBAFloat);request.WaitForCompletion();Require(!request.hasError,"Lazy field readback failed");float max=0;
        for(int layer=0;layer<request.layerCount;++layer){var data=request.GetData<float>(layer);for(int i=3;i<data.Length;i+=4)max=Mathf.Max(max,data[i]);}return max;
    }
    private static void Require(bool test,string message){if(!test)throw new InvalidOperationException(message);}
    [Serializable]private sealed class Report
    {
        public bool passed,delayedClock,recoveredReleased,reentered,sharedEmptyRetained,mixedAtlas,blueprintAdapter,blueprintVisible,narrowContact,budgetFailureRetainsNeighbor,staleRecoveryRetained,gameHostTested,wholeHouseSupported;
        public int frames,owners;public long peakFieldBytes,restAtlasBytes;public double idleDelta,maxImageDelta,maxForegroundDelta;
    }
}

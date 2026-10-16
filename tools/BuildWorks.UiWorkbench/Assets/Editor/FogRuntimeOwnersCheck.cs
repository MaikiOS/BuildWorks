using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeOwnersCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-owners"); Directory.CreateDirectory(output);
        var prefabs = new GameObject[3]; var materials = new Material[3]; var visuals = new BlueprintFogVisual[3];
        Vector3[] scales = { new Vector3(.2f,2,2), new Vector3(2,.2f,2), new Vector3(.2f,.2f,2) };
        Vector3[] positions = { Vector3.up, Vector3.zero, new Vector3(0,1.3f,0) };
        Color[] colors = { Color.red, Color.green, Color.blue };
        Vector3 start = new Vector3(-1.4f,.85f,0); Vector2 body = new Vector2(.3f,.95f);
        long total = 0; int sourceCells = 0, frames = 0; float contact = 0;
        double worstImage = 0, worstForeground = 0, minimumEmptyDelta = double.PositiveInfinity, translatedDelta = 0;
        Color[] nearContact = null;
        ColorSpace previousSpace = PlayerSettings.colorSpace;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Compact owner camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(3,2.2f,-4); camera.transform.LookAt(Vector3.up);
        camera.fieldOfView=49; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.15f,.2f,.3f);
        camera.allowHDR=true; camera.renderingPath=RenderingPath.DeferredShading; camera.depthTextureMode=DepthTextureMode.Depth;
        var probe=cameraObject.AddComponent<FogRenderProbe>();
        var texture=new Texture2D(8,8,TextureFormat.RGBA32,false);
        texture.SetPixels(Enumerable.Range(0,64).Select(i=>(i/8+i%8)%2==0?Color.white:Color.gray).ToArray()); texture.Apply();
        BlueprintFogVisual reference=null;
        try
        {
            PlayerSettings.colorSpace=ColorSpace.Linear; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(QualitySettings.activeColorSpace==ColorSpace.Linear,"Compact render requires native Linear color space");
            for (int i=0;i<3;++i)
            {
                prefabs[i] = GameObject.CreatePrimitive(PrimitiveType.Cube); prefabs[i].name = "Owned " + i;
                prefabs[i].transform.localScale = scales[i]; prefabs[i].SetActive(false);
                materials[i] = new Material(Shader.Find("Standard")); materials[i].color = colors[i];
                materials[i].mainTexture=texture;
                prefabs[i].GetComponent<Renderer>().sharedMaterial = materials[i];
            }
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root,"src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var pool = new BlueprintFogChannels(8, BlueprintFogChannels.MaximumFieldBudget))
            {
                for (int i=0;i<3;++i)
                {
                    visuals[i] = new BlueprintFogVisual(assets,prefabs[i],positions[i],i==2?Quaternion.Euler(0,90,0):Quaternion.identity,
                        i==2?new Vector3(1.2f,1,.8f):Vector3.one,null,Vector3.zero,start,body,pool,"part-"+i,true);
                    var source = visuals[i].Source;
                    Require(visuals[i].Root.GetComponentsInChildren<Collider>(true).Length == 0,"Compact visual has physical colliders");
                    Require(source.Shape.width<144 && source.Shape.height<112 && source.Shape.depth<120,"Source still uses full wall allocation");
                    Vector3 voxel = source.Maximum-source.Minimum;
                    Require(voxel.x/source.Shape.width <= 4.8f/144+.00001f && voxel.y/source.Shape.height <= 4.5f/112+.00001f &&
                        voxel.z/source.Shape.depth <= 3.4f/120+.00001f,"Compact source lost physical detail");
                    foreach(var part in source.Parts) foreach(int[] indices in part.Data.Submeshes) foreach(int index in indices)
                    {
                        Vector3 point = part.ToMetres.MultiplyPoint3x4(part.Data.Positions[index]);
                        Require(point.x>source.Minimum.x && point.y>source.Minimum.y && point.z>source.Minimum.z &&
                            point.x<source.Maximum.x && point.y<source.Maximum.y && point.z<source.Maximum.z,"Compact source clipped native geometry");
                    }
                    total += visuals[i].Simulation.AllocatedFieldBytes;
                    sourceCells += source.Shape.width*source.Shape.height*source.Shape.depth;
                }
                Require(pool.Count==3 && pool.FieldBytes==total && total<BlueprintFogChannels.MaximumFieldBudget,"Three owners exceed byte budget");
                reference=new BlueprintFogVisual(assets,prefabs[0],positions[0],Quaternion.identity,Vector3.one,null,Vector3.zero,start,body);
                void CompareImage(int tick)
                {
                    foreach(var visual in visuals) visual.Root.SetActive(false); reference.Root.SetActive(false);
                    Color[] empty=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>Graphics.Blit(image,target),output,tick+"-empty");
                    reference.Root.SetActive(true);
                    Color[] original=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>reference.RenderVolume(camera,image,target,new Vector3(-4,1,0),Vector3.right,0),output,tick+"-original");
                    reference.Root.SetActive(false); visuals[0].Root.SetActive(true);
                    Color[] compact=FogRuntimePairCheck.Capture(camera,probe,(image,target)=>visuals[0].RenderVolume(camera,image,target,new Vector3(-4,1,0),Vector3.right,0),output,tick+"-compact");
                    if(tick==60) nearContact=compact;
                    worstImage=Math.Max(worstImage,FogRuntimePairCheck.Delta(original,compact));
                    double foreground=0, absent=0; int pixels=0;
                    for(int p=0;p<original.Length;++p)
                    {
                        double blank=Math.Abs(original[p].r-empty[p].r)+Math.Abs(original[p].g-empty[p].g)+Math.Abs(original[p].b-empty[p].b);
                        if(blank<.06) continue;
                        foreground+=Math.Abs(original[p].r-compact[p].r)+Math.Abs(original[p].g-compact[p].g)+Math.Abs(original[p].b-compact[p].b);
                        absent+=blank; ++pixels;
                    }
                    Require(pixels>100,"Reference has no measurable foreground");
                    worstForeground=Math.Max(worstForeground,foreground/(pixels*3));
                    minimumEmptyDelta=Math.Min(minimumEmptyDelta,absent/(pixels*3));
                    foreach(var visual in visuals) visual.Root.SetActive(true); ++frames;
                }
                CompareImage(0);
                bool borrowedRejected=false;
                try { pool.TryAcquire("borrowed",visuals[0].Simulation.AllocatedFieldBytes,()=>visuals[0].Simulation,out _); }
                catch(InvalidOperationException) { borrowedRejected=true; }
                Require(borrowedRejected && pool.Count==3 && visuals[0].Simulation.Smoke,"Duplicate factory disposed original owner");
                BlueprintFogSimulation Make(bool full) => new BlueprintFogSimulation(assets,visuals[0].Source.Shape,visuals[0].Source.Pigment,visuals[0].Source.Noise,
                    visuals[0].Source.Minimum-new Vector3(1.2f,0,1),visuals[0].Source.Maximum+new Vector3(1.2f,.6f,1),
                    visuals[0].Source.Minimum,visuals[0].Source.Maximum,start,37,full?(Vector3Int?)null:visuals[0].Simulation.GridSize);
                long bytes=visuals[0].Simulation.AllocatedFieldBytes;
                using(var tight=new BlueprintFogChannels(8,bytes))
                {
                    Require(tight.TryAcquire("A",bytes,()=>Make(false),out var own),"Exact byte budget rejected");
                    bool called=false;
                    Require(!tight.TryAcquire("B",bytes,()=>{called=true;return Make(false);},out _) && !called && tight.FieldBytes==bytes && own.Smoke,
                        "Byte exhaustion allocated or evicted an owner");
                }
                BlueprintFogSimulation rejected=null; bool mismatchRejected=false;
                try { pool.TryAcquire("underreserved",20480,()=>rejected=Make(true),out _); }
                catch(InvalidOperationException) { mismatchRejected=true; }
                Require(mismatchRejected && rejected!=null && !rejected.Smoke && pool.Count==3 && pool.FieldBytes==total,"Reservation mismatch leaked or damaged owners");
                bool largeRejected=false;
                try { using(var large=new BlueprintFogVisual(assets,prefabs[0],positions[0],Quaternion.identity,Vector3.one*100,null,Vector3.zero,start,body,pool,"large",true)) { } }
                catch(InvalidOperationException) { largeRejected=true; }
                Require(largeRejected && pool.Count==3 && pool.FieldBytes==total,"Oversized part altered live owners");
                for(int tick=1;tick<=90;++tick)
                {
                    Vector3 actor=Vector3.Lerp(start,new Vector3(0,.85f,0),Mathf.Min(1,tick/30f));
                    foreach(var visual in visuals) visual.AdvanceTo(tick*BlueprintFogSimulation.Step,actor,body,true);
                    reference.AdvanceTo(tick*BlueprintFogSimulation.Step,actor,body,true);
                    if(tick==60 || tick==90)
                        for(int i=0;i<3;++i)
                        {
                            float[] smoke=Read(visuals[i].Simulation.Smoke); float damage=MaxAlpha(Read(visuals[i].Simulation.Surface));
                            Require(damage>.5f && MaxAlpha(smoke)>0,"Wall/floor/beam simultaneous hole absent"); contact=Mathf.Max(contact,damage);
                            for(int c=0;c<3;++c) if(c!=i) for(int p=c;p<smoke.Length;p+=4) Require(smoke[p]==0,"Pigment moved to a different part");
                        }
                    if(tick==60 || tick==90) CompareImage(tick);
                }
                Require(worstImage<.005,"Compact visual differs from approved one-wall path");
                Require(worstForeground<.03 && minimumEmptyDelta>.03,"Compact foreground/absent-wall negative control failed");
                reference.Dispose(); reference=null;
                float[][] before={Read(visuals[1].Simulation.Smoke),Read(visuals[1].Simulation.Surface),Read(visuals[2].Simulation.Smoke),Read(visuals[2].Simulation.Surface)};
                long removed=visuals[0].Simulation.AllocatedFieldBytes; visuals[0].Dispose(); visuals[0]=null;
                Require(pool.Count==2 && pool.FieldBytes==total-removed && before[0].SequenceEqual(Read(visuals[1].Simulation.Smoke)) &&
                    before[1].SequenceEqual(Read(visuals[1].Simulation.Surface)) && before[2].SequenceEqual(Read(visuals[2].Simulation.Smoke)) &&
                    before[3].SequenceEqual(Read(visuals[2].Simulation.Surface)),"Building/removing wall changed floor/beam matter");
                for(int tick=91;tick<=420;++tick) for(int i=1;i<3;++i)
                    visuals[i].AdvanceTo(tick*BlueprintFogSimulation.Step,new Vector3(3,.85f,3),body,false);
                for(int i=1;i<3;++i)
                {
                    Require(MaxAlpha(Read(visuals[i].Simulation.Smoke))==0 && MaxAlpha(Read(visuals[i].Simulation.Surface))==0,"Compact owner did not return");
                    Require(!pool.TryReleaseRecovered("part-"+i),"Missing recovery query accepted");
                    AsyncGPUReadback.WaitAllRequests(); Require(pool.TryReleaseRecovered("part-"+i),"Recovered owner not released");
                    visuals[i].Dispose(); visuals[i]=null;
                }
                Require(pool.Count==0 && pool.FieldBytes==0,"Compact owners survived cleanup");
                Vector3 shift=new Vector3(2500,45,-1900); camera.transform.position+=shift;
                using(var translated=new BlueprintFogVisual(assets,prefabs[0],shift+positions[0],Quaternion.identity,Vector3.one,null,shift,shift+start,body,pool,"translated",true))
                {
                    for(int tick=1;tick<=60;++tick)
                        translated.AdvanceTo(tick*BlueprintFogSimulation.Step,shift+Vector3.Lerp(start,new Vector3(0,.85f,0),Mathf.Min(1,tick/30f)),body,true);
                    Color[] image=FogRuntimePairCheck.Capture(camera,probe,(source,target)=>translated.RenderVolume(camera,source,target,shift+new Vector3(-4,1,0),Vector3.right,0),output,"60-translated-compact");
                    translatedDelta=FogRuntimePairCheck.Delta(nearContact,image);
                    Require(translatedDelta<.005 && MaxAlpha(Read(translated.Simulation.Surface))>.5f,"Distant world coordinates broke compact source/contact");
                }
                Require(pool.Count==0 && pool.FieldBytes==0,"Translated owner survived cleanup");
            }
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonUtility.ToJson(new Report {
                passed=true,owners=3,ticks=420,frames=frames,maxImageDelta=worstImage,maxForegroundDelta=worstForeground,
                minimumEmptyDelta=minimumEmptyDelta,translatedDelta=translatedDelta,activeFieldBytes=total,sourceCells=sourceCells,contactDamage=contact,
                geometryRetained=true,physicalDetailRetained=true,pigmentIndependent=true,removalIsolated=true,
                budgetRejectBeforeFactory=true,reservationMismatchDisposed=true,largeRejected=true,recoveredReleased=true,
                gameHostTested=false,wholeHouseSupported=false,volumeForThreeTested=false },true));
        }
        finally
        {
            reference?.Dispose(); probe.Render=null;
            foreach(var visual in visuals) visual?.Dispose();
            foreach(var prefab in prefabs) if(prefab) UnityEngine.Object.DestroyImmediate(prefab);
            foreach(var material in materials) if(material) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(cameraObject);
            PlayerSettings.colorSpace=previousSpace; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
    private static float[] Read(RenderTexture field)
    {
        var request=AsyncGPUReadback.Request(field,0,TextureFormat.RGBAFloat); request.WaitForCompletion();
        Require(!request.hasError,"Owner readback failed"); var result=new float[field.width*field.height*field.volumeDepth*4]; int offset=0;
        for(int layer=0;layer<request.layerCount;++layer) {var data=request.GetData<float>(layer); for(int i=0;i<data.Length;++i) result[offset+i]=data[i]; offset+=data.Length;}
        Require(offset==result.Length && result.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)),"Incomplete/nonfinite owner field"); return result;
    }
    private static float MaxAlpha(float[] data) {float maximum=0;for(int i=3;i<data.Length;i+=4) maximum=Mathf.Max(maximum,data[i]);return maximum;}
    private static void Require(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
    [Serializable] private sealed class Report
    {
        public bool passed,geometryRetained,physicalDetailRetained,pigmentIndependent,removalIsolated,budgetRejectBeforeFactory,
            reservationMismatchDisposed,largeRejected,recoveredReleased,gameHostTested,wholeHouseSupported,volumeForThreeTested;
        public int owners,ticks,sourceCells,frames; public long activeFieldBytes; public float contactDamage;
        public double maxImageDelta,maxForegroundDelta,minimumEmptyDelta,translatedDelta;
    }
}

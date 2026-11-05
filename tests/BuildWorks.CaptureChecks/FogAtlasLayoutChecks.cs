using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEngine;

namespace FogAtlasChecks
{
    public static class Runner
    {
        private static int checks;
        private static void Check(bool value,string message) {if(!value)throw new Exception(message);++checks;}
        private static void Refuses<T>(Action action,string message) where T:Exception
        {
            try {action();} catch(T e) {Check(e.Message.Contains(message),"Wrong refusal: "+e.Message);return;}
            throw new Exception("Expected refusal: "+message);
        }
        private static object Field(BlueprintFogAtlas atlas,string name) => typeof(BlueprintFogAtlas).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(atlas);
        private static BlueprintFogVisual[] Owners(int count,float spacing,int width=4,bool unique=false)
        {
            return Enumerable.Range(0,count).Select(i=>new BlueprintFogVisual {
                Source=new BlueprintFogSource {Shape=new Texture3D {width=width,height=4,depth=4},ShapeHash=unique?"shape"+i:"shape"},
                VolumeBounds=new Bounds(new Vector3(i<count/2?0:spacing,0,0),new Vector3(1,1,1))
            }).ToArray();
        }
        private static void VolumeRefuses(BlueprintFogAtlas atlas,BlueprintFogVisual[] owners,bool published)
        {
            var snapshots=new BlueprintFogChannels.SnapshotChannel[owners.Length];
            if(published)atlas.PreparePublished(owners,snapshots);
            int creates=RenderTexture.Creates,dispatches=ComputeShader.Dispatches,blits=Graphics.Blits,uploads=ComputeBuffer.Uploads;
            Refuses<InvalidOperationException>(()=>atlas.Render(new Camera(),null,null,owners,default,default,0,published?snapshots:null),"bounded integration work");
            Check(RenderTexture.Creates==creates && ComputeShader.Dispatches==dispatches && Graphics.Blits==blits,"Rejected volume reached allocation/dispatch/presentation.");
            Check(ComputeBuffer.Uploads==uploads+(published?1:0),"Published Render binding upload boundary changed.");
        }
        public static int Run()
        {
            checks=0;var assets=new BlueprintFogAssets();var pool=new BlueprintFogChannels();
            var union=new Bounds(default,new Vector3(1,1,1));union.Encapsulate(new Bounds(new Vector3(30,0,0),new Vector3(1,1,1)));
            Check(union.size.x==31 && union.size.y==1 && union.size.z==1 && Mathf.CeilToInt(union.size.magnitude/.025f)==1242,"Inert bounds/math fixture does not reproduce the known31m union.");
            Refuses<ArgumentException>(()=>new BlueprintFogAtlas(assets,null),"snapshot pool");
            var thin=Enumerable.Range(0,32).Select(_=>new Vector3Int(3,4,5)).ToArray();var packed=new Vector3Int[32];
            var packedSize=BlueprintFogAtlas.Layout(thin,32,packed);
            Check(packedSize.x<=32 && packedSize.y<=32 && packedSize.z<=32 && (long)packedSize.x*packedSize.y*packedSize.z*18<=BlueprintFogAtlas.MaximumBytes,"Public layout exceeded axis/memory budget.");
            bool disjoint=true;
            for(int i=0;i<thin.Length;i++)for(int j=i+1;j<thin.Length;j++)
                disjoint &= packed[i].x+thin[i].x<=packed[j].x || packed[j].x+thin[j].x<=packed[i].x || packed[i].y+thin[i].y<=packed[j].y || packed[j].y+thin[j].y<=packed[i].y || packed[i].z+thin[i].z<=packed[j].z || packed[j].z+thin[j].z<=packed[i].z;
            Check(disjoint,"Actual layout overlaps distinct blocks.");
            Refuses<InvalidOperationException>(()=>BlueprintFogAtlas.Layout(Enumerable.Repeat(new Vector3Int(128,128,128),4).ToArray(),2048,new Vector3Int[4]),"render memory budget");
            Refuses<InvalidOperationException>(()=>BlueprintFogAtlas.Layout(new[]{new Vector3Int(int.MaxValue,3,3)},2048,new Vector3Int[1]),"device axis budget");
            using(var atlas=new BlueprintFogAtlas(assets,pool))
            {
                var owners=Owners(32,30);
                // RED on305: independently valid clusters exceed the unused global volume work budget.
                atlas.PreparePublishedLayout(owners);
                Check((int)Field(atlas,"layoutSourceCount")==32,"32 surface owners were not admitted.");
                Check(((List<Vector3Int>)Field(atlas,"layoutBlocks")).Count==1,"Equal immutable shapes were not shared.");
                var offsets=((Vector3Int[])Field(atlas,"offsets")).ToArray();
                Check(offsets.Take(32).All(v=>v==offsets[0]),"Shared shape offsets differ.");
                VolumeRefuses(atlas,owners,true);
                var size=(Vector3Int)Field(atlas,"size");int creates=RenderTexture.Creates;
                foreach(var owner in owners)owner.VolumeBounds=new Bounds(new Vector3(1000,0,0),new Vector3(1,1,1));
                owners[0].VolumeBounds=new Bounds(default,new Vector3(1,1,1));
                atlas.PreparePublishedLayout(owners);
                Check(RenderTexture.Creates==creates && (Vector3Int)Field(atlas,"size")==size,"Spacing changed surface allocation.");
                VolumeRefuses(atlas,owners,true);
                var different=Owners(32,80);atlas.PreparePublishedLayout(different);
                Check((Vector3Int)Field(atlas,"size")==size,"Cold layout depends on world spacing.");
                VolumeRefuses(atlas,different,true);
                Refuses<ArgumentException>(()=>atlas.PreparePublishedLayout(null),"owner count");
                Refuses<ArgumentException>(()=>atlas.PreparePublishedLayout(Array.Empty<BlueprintFogVisual>()),"owner count");
                Refuses<ArgumentException>(()=>atlas.PreparePublishedLayout(Owners(33,0)),"owner count");
                Refuses<ArgumentException>(()=>atlas.PreparePublishedLayout(new BlueprintFogVisual[]{null}),"source missing");
                var missing=Owners(1,0);missing[0].Source=null;
                Refuses<ArgumentException>(()=>atlas.PreparePublishedLayout(missing),"source missing");
                missing=Owners(1,0);missing[0].Source.Shape=null;
                Refuses<ArgumentException>(()=>atlas.PreparePublishedLayout(missing),"source missing");
                missing=Owners(1,0);UnityEngine.Object.DestroyImmediate(missing[0].Source.Shape);
                Refuses<ArgumentException>(()=>atlas.PreparePublishedLayout(missing),"source missing");
                SystemInfo.maxTexture3DSize=3;
                Refuses<InvalidOperationException>(()=>atlas.PreparePublishedLayout(Owners(1,0)),"device axis budget");
                SystemInfo.maxTexture3DSize=2048;
                var large=Owners(1,0,512);large[0].Source.Shape.height=large[0].Source.Shape.depth=512;
                Refuses<InvalidOperationException>(()=>atlas.PreparePublishedLayout(large),"render memory budget");
                pool.SnapshotArena.RenderBytes=BlueprintFogAtlas.MaximumBytes-1;
                Refuses<InvalidOperationException>(()=>atlas.PreparePublishedLayout(Owners(1,0)),"render memory budget");
                pool.SnapshotArena.RenderBytes=0;
                var many=Owners(32,0,160,true);foreach(var owner in many)owner.Source.Shape.height=owner.Source.Shape.depth=160;
                Refuses<InvalidOperationException>(()=>atlas.PreparePublishedLayout(many),"render memory budget");
                atlas.PreparePublishedLayout(different);
                var priorOffsets=((Vector3Int[])Field(atlas,"offsets")).ToArray();
                var priorSources=((Texture3D[])Field(atlas,"layoutSources")).ToArray();
                var priorBlocks=((List<Vector3Int>)Field(atlas,"layoutBlocks")).ToArray();
                var priorShape=(RenderTexture)Field(atlas,"shape");
                int priorLimit=(int)Field(atlas,"layoutLimit");
                ((BlueprintFogVisual[])Field(atlas,"packedOwners"))[0]=different[0];
                RenderTexture.FailNextCreate=true;
                Refuses<InvalidOperationException>(()=>atlas.PreparePublishedLayout(Owners(32,30,9)),"allocation failed");
                Check(priorOffsets.SequenceEqual((Vector3Int[])Field(atlas,"offsets")),"Allocation failure changed offsets.");
                Check(priorSources.SequenceEqual((Texture3D[])Field(atlas,"layoutSources")),"Allocation failure changed source cache.");
                Check(priorBlocks.SequenceEqual((List<Vector3Int>)Field(atlas,"layoutBlocks")),"Allocation failure changed block cache.");
                Check((int)Field(atlas,"layoutLimit")==priorLimit && ReferenceEquals(((BlueprintFogVisual[])Field(atlas,"packedOwners"))[0],different[0]),"Allocation failure changed limit/packed-owner cache.");
                Check((int)Field(atlas,"layoutSourceCount")==32 && ReferenceEquals(priorShape,Field(atlas,"shape")) && priorShape.Alive && !priorShape.Released,"Allocation failure retired previous atlas.");
                atlas.PreparePublishedLayout(Owners(32,30,9));
                Check(!priorShape.Alive && (Vector3Int)Field(atlas,"size")!=size,"Successful retry did not commit new allocation.");
                Check(((BlueprintFogVisual[])Field(atlas,"packedOwners")).All(owner=>owner==null),"Successful layout did not invalidate packed-owner cache.");
                var collision=Owners(2,70);collision[1].Source.Shape.Data=new byte[]{2};
                atlas.PreparePublishedLayout(collision);
                Check(((List<Vector3Int>)Field(atlas,"layoutBlocks")).Count==2,"Hash collision aliased unequal density bytes.");
                Refuses<ArgumentException>(()=>atlas.PreparePublished(collision,new BlueprintFogChannels.SnapshotChannel[1]),"binding count");
            }
            using(var legacy=new BlueprintFogAtlas(assets))
            {
                VolumeRefuses(legacy,Owners(32,30),false);
                var wide=Owners(1,0);wide[0].VolumeBounds=new Bounds(default,new Vector3(80,1,1));
                VolumeRefuses(legacy,wide,false);
            }
            return checks;
        }
    }
}

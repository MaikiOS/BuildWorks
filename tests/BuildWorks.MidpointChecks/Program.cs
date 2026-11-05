using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object=UnityEngine.Object;
using HarmonyLib;

internal static class Program
{
    private static void Require(bool value,string message) {if(!value)throw new InvalidOperationException(message);}
    private static Transform Native(Piece p,float x) {var g=new GameObject("native"){tag="snappoint"};g.transform.SetParent(p.transform,false);g.transform.localPosition=Vector3.right*x;return g.transform;}
    private static List<Transform> Query(Piece p) {var r=new List<Transform>();p.GetSnapPoints(r);VanillaMidpointSnapPoints.Append(p,r);return r;}
    private static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);
        try
        {
            AdmissionChecks.Run();
            if(args.Length>1&&args[1]=="--admission") return;
            foreach(int count in new[]{20,80,160}) Measure(count);
            if(args.Length>1&&args[1]=="--measure") return;
            var root=new GameObject("regression"); var p=root.AddComponent<Piece>();var a=Native(p,0);var b=Native(p,2);
            var first=Query(p);var middle=first.Single(VanillaMidpointSnapPoints.IsGenerated);
            int writes=GameObject.Writes;var warm=Query(p);
            Require(warm.SequenceEqual(first),"warm query changed identity/order");
            if(args.Length<2||args[1]!="--stale") Require(GameObject.Writes==writes,"REPRO: unchanged geometry rewrites midpoint transforms/active state");
            root.transform.position=Vector3.right*5;writes=GameObject.Writes;Query(p);if(args.Length<2||args[1]!="--stale") Require(GameObject.Writes==writes,"root world move rebuilt local geometry");
            b.localPosition=Vector3.right*4;Require(Query(p).Contains(middle)&&middle.localPosition.x==2,"native local position not invalidated");
            a.localRotation=new Quaternion(0,1,0,0);Query(p);Require(middle.localRotation.y!=0,"native rotation not invalidated");
            b.tag="Untagged";var fewer=Query(p);Require(!fewer.Contains(middle)&&!middle.CompareTag("snappoint")&&!middle.gameObject.activeSelf,"REPRO: stale midpoint survived native.Count<2");
            b.tag="snappoint";Require(Query(p).Contains(middle),"native tag restored without same midpoint identity");
            var duplicateSeed=new List<Transform>{a,a,middle,middle,null};
            VanillaMidpointSnapPoints.Append(p,duplicateSeed);
            Require(duplicateSeed.SequenceEqual(new Transform[]{a,a,middle,middle,null}),"active preseeded duplicates/null changed");
            b.tag="Untagged";VanillaMidpointSnapPoints.Append(p,duplicateSeed);
            Require(duplicateSeed.SequenceEqual(new Transform[]{a,a,null}),"retired point remained in preseeded duplicate list");b.tag="snappoint";Query(p);
            b.SetSiblingIndex(0);Query(p);var c=Native(p,8);Query(p);
            c.SetSiblingIndex(0);var ordered=Query(p).Where(VanillaMidpointSnapPoints.IsGenerated).ToArray();
            Require(ordered.Length==2&&ordered[0].localPosition.x==6&&ordered[1].localPosition.x==2,"immediate child order did not invalidate edge order");
            Object.DestroyImmediate(c.gameObject);Query(p);
            var other=new GameObject("other");b.SetParent(other.transform,false);Require(!Query(p).Any(VanillaMidpointSnapPoints.IsGenerated),"reparent left stale midpoint");b.SetParent(root.transform,false);Query(p);
            Object.DestroyImmediate(middle.gameObject);Require(Query(p).Any(t=>t&&VanillaMidpointSnapPoints.IsGenerated(t)),"destroyed generated node not rebuilt");
            var reparented=Query(p).First(VanillaMidpointSnapPoints.IsGenerated);reparented.SetParent(other.transform,false);
            Require(Query(p).Any(t=>t&&VanillaMidpointSnapPoints.IsGenerated(t))&&!reparented.gameObject.activeSelf,"reparented generated node not retired/rebuilt");
            Object.DestroyImmediate(root);Object.DestroyImmediate(other);
            var many=new GameObject("limit");var manyPiece=many.AddComponent<Piece>();for(int i=0;i<40;++i)Native(manyPiece,i*2);
            Require(Query(manyPiece).Count(VanillaMidpointSnapPoints.IsGenerated)==32,"generated point limit changed");Object.DestroyImmediate(many);
            var weak=DestroyWithoutCallback();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            Require(!weak.IsAlive,"never-active simulated destroy retained cache without OnDestroy");
            WorldSnapCollectionAcceptance.Run(args[0]);
            Console.WriteLine("PASS midpoint invalidation/warm writes + existing actual-Harmony WorldSnapCollectionAcceptance; inert hierarchy, not Unity/Valheim/FPS.");
        }
        finally {VanillaMidpointSnapPoints.RemoveAll();BlueprintWorldSnapPoints.RemoveAll();}
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference DestroyWithoutCallback()
    {
        var root=new GameObject("never-active-double");root.SetActive(false);var p=root.AddComponent<Piece>();Native(p,0);Native(p,2);Query(p);
        var weak=new WeakReference(root.GetComponent<VanillaMidpointSnapPoints>());
        Object.SuppressOnDestroy=true;try{Object.DestroyImmediate(root);}finally{Object.SuppressOnDestroy=false;}
        return weak;
    }
    private static void Measure(int count)
    {
        var roots=new List<GameObject>();var pieces=new List<Piece>();
        for(int i=0;i<count;++i){var go=new GameObject("bench");roots.Add(go);var p=go.AddComponent<Piece>();for(int j=0;j<4;++j)Native(p,j*2);pieces.Add(p);}
        var result=new List<Transform>(4000);
        Action collect=()=>{result.Clear();foreach(var p in pieces)for(int hit=0;hit<2;++hit){p.GetSnapPoints(result);VanillaMidpointSnapPoints.Append(p,result);}};
        collect();var expected=result.ToArray();AppDomain.MonitoringIsEnabled=true;int writes=GameObject.Writes;long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        var watch=Stopwatch.StartNew();for(int i=0;i<80;++i)collect();watch.Stop();long bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;
        Console.WriteLine($"DIRECT warm 80queries/{count}pieces/2hits: {watch.Elapsed.TotalMilliseconds:F3}ms, allocated={bytes}, writes={GameObject.Writes-writes}, points={result.Count}");
        var harmony=new Harmony(BuildWorksPlugin.PluginGuid);
        try
        {
            harmony.Patch(AccessTools.Method(typeof(Piece),nameof(Piece.GetSnapPoints),new[]{typeof(List<Transform>)}),
                postfix:new HarmonyMethod(typeof(WorldSnapCollectionAcceptance),"AppendPostfix"));
            harmony.CreateClassProcessor(typeof(BlueprintWorldSnapPoints.CollectionPatch)).Patch();
            foreach(var p in pieces){Piece.NearbyPieces.Add(p);Piece.NearbyPieces.Add(p);}
            var visited=new List<Piece>(160);
            Action scoped=()=>{result.Clear();visited.Clear();Piece.GetSnapPoints(Vector3.zero,10,result,visited);};
            scoped();writes=GameObject.Writes;before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            watch.Restart();for(int i=0;i<80;++i)scoped();watch.Stop();bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;
            Require(result.SequenceEqual(expected)&&visited.SequenceEqual(Piece.NearbyPieces),"scoped measure changed exact ordered points/piece visits");
            Console.WriteLine($"SCOPED warm 80queries/{count}pieces/2hits: {watch.Elapsed.TotalMilliseconds:F3}ms, allocated={bytes}, writes={GameObject.Writes-writes}, points={result.Count}, visits={visited.Count}, nativePerPiece=4, generatedPerPiece=3, orderedExact=true; ghost selector not measured");
        }
        finally {Piece.NearbyPieces.Clear();harmony.UnpatchSelf();}
        foreach(var go in roots)Object.DestroyImmediate(go);VanillaMidpointSnapPoints.RemoveAll();
    }
}

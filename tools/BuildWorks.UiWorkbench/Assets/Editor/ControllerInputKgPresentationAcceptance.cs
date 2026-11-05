using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using Object=UnityEngine.Object;

internal static partial class ControllerInputAcceptance
{
    internal static IEnumerable<object> RunKgPresentation(Camera template,TMP_Text font,string output)
    {
        byte[] red=Png(Color.red),blue=Png(Color.blue);
        string Image(byte[] bytes)=>Convert.ToBase64String(bytes);
        CompositeBlueprintStore.Blueprint Source(string extra,string prefab="wood_wall",string state=null)=>ExternalBlueprintLibrary.Parse(
            "Name: source\nAuthor: '<b>QA_FILTER_WORD</b>'\nDescription: 'description </noparse><size=999>literal'\nObjects: [{Id: "+prefab+
            (state==null?"":", ZDOData: "+state)+"}]\n"+extra,"source.yml");
        var provider=Splatform.PlatformManager.DistributionPlatform.PrivilegeProvider;
        var source=Source("Icon: "+Image(red)+"\nPreviews: ["+Image(blue)+"]\n");
        var evidence=source.sourceDocuments.Single().ToGeometry();
        byte[] raw=BlueprintExportPair.Snapshot(source).Native;
        using(var renderer=new BlueprintThumbnailRenderer())
        {
            try
            {
                provider.Granted=true;
                Require(BlueprintSourcePresentation.Metadata(source).Contains("＜b＞***＜/b＞") &&
                    !BlueprintSourcePresentation.Metadata(source).Contains("<size"),"KG metadata is unfiltered or rich-text active");
                Require(BlueprintSourcePresentation.Literal(new string('a',127)+"😀",128,1)==new string('a',127) &&
                    BlueprintSourcePresentation.Literal(new string('\r',5000)+"marker",128,1)=="" &&
                    !BlueprintSourcePresentation.Literal("literal\\nnot a line",128,1).Contains("\\") &&
                    BlueprintSourcePresentation.Literal("1\r\n2\u20283\u20294\n5\n6\n7",1024,6).Split('\n').Length==6,
                    "Metadata character/line/surrogate boundary failed");
                Sprite icon=renderer.GetCached(source);
                Require(icon && icon.texture.GetPixel(0,0).r>.9f,"KG Icon priority failed");
                for(int i=0;i<20;i++)Require(renderer.GetCached(source)==icon,"Warm KG image decoded again");
                Require(ReferenceEquals(evidence,source.sourceDocuments.Single().ToGeometry()),"Warm metadata reparsed immutable source");
                provider.Granted=false;
                Require(!renderer.GetCached(source),"UGC revoke retained cached embedded image");
                Require(!BlueprintSourcePresentation.Images(source).Any(),"UGC revoke allowed embedded candidate");
                Require(!BlueprintSourcePresentation.Metadata(source).Contains("literal"),"UGC revoke retained raw description");
                provider.Granted=true;
                Require(renderer.GetCached(source),"UGC grant did not restore eligible image");
                var swapped=Source("Icon: "+Image(blue)+"\n");swapped.id=source.id;
                Require(renderer.GetCached(swapped)?.texture.GetPixel(0,0).b>.9f,"Changed source retained old image for same blueprint ID");
                var waiting=Source("Icon: "+Image(red.Take(33).ToArray())+"\n");waiting.id=Guid.NewGuid().ToString("N");
                renderer.GetOrQueue(waiting,new Piece[]{null});
                var replacement=Source("Icon: "+Image(blue)+"\n");replacement.id=waiting.id;
                Require(renderer.GetCached(replacement)?.texture.GetPixel(0,0).b>.9f,"Replacement image did not load");
                var pending=(System.Collections.ICollection)typeof(BlueprintThumbnailRenderer).GetField("pending",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(renderer);
                Require(pending.Count==0 && !renderer.ProcessOne(out _,out _),"Old-source pending render survived source replacement");

                var fallback=Source("Icon: "+Image(red.Take(33).ToArray())+"\nPreviews: ["+Image(blue)+"]\n");
                fallback.id=Guid.NewGuid().ToString("N");
                Require(renderer.GetCached(fallback)?.texture.GetPixel(0,0).b>.9f,"Damaged Icon blocked valid Preview fallback");
                var oversized=(byte[])red.Clone();oversized[16]=0;oversized[17]=0;oversized[18]=8;oversized[19]=1;
                var large=Source("Icon: "+Image(oversized)+"\nPreviews: ["+Image(blue)+"]\n");large.id=Guid.NewGuid().ToString("N");
                Require(renderer.GetCached(large)?.texture.GetPixel(0,0).b>.9f,"Core-valid >2048 image reached GPU or blocked fallback");
                foreach(string sign in new[]{"sign",ForeignHash("sign").ToString(System.Globalization.CultureInfo.InvariantCulture)})
                {
                    var unsafeSource=Source("Icon: "+Image(red)+"\n",sign);unsafeSource.importPreview=blue;
                    Require(!BlueprintSourcePresentation.Images(unsafeSource).Any(),"Raw/source sign bypassed imported image policy");
                }
                var foreign=Source("Icon: "+Image(red)+"\n");
                foreign.parts[0].foreignData=new CompositeBlueprintStore.ForeignPartData{format=KgBlueprint.Format,zdo="AAAAAAAAAA=="};
                Require(!BlueprintSourcePresentation.Images(foreign).Any(),"Unprojected state allowed embedded image");
                Require(BlueprintForeignZdo.TryReadKg("AAAAAAAAAA==",out var empty),"Empty KG packet rejected");
                string custom=empty.WriteKg(new[]{new KeyValuePair<int,object>(ForeignHash("customText"),"unknown")});
                var unknown=Source("Icon: "+Image(red)+"\n",state:custom);unknown.parts[0].foreignData=null;
                Require(!BlueprintSourcePresentation.Images(unknown).Any(),"Stale native copy bypassed raw-source state guard");
                Require(BlueprintExportPair.Snapshot(source).Native.SequenceEqual(raw),"Presentation mutated native source");

                var binaryControl=ExternalBlueprintLibrary.LoadFile(Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../../artifacts/point-2.14-20261028/fresh-capture/capture-c17ea102/controlled-kg-writer.oprint"))).Single();
                Require(binaryControl.importError==null && !BlueprintSourcePresentation.Images(binaryControl).Any(),
                    "Binary source signs bypassed image guard");
                var binaryModel=binaryControl.sourceDocuments.Single().ToGeometry().Kg;
                string binaryOriginal=binaryModel.Text;
                // Derived inert fixture keeps the declared old PNG and replaces its rows; no native capture.
                byte[] binaryPreview=binaryModel.WriteBinaryObjects(new[]{new KgBlueprint.Piece(0,"wood_wall",new Point3(0,0,0),new Point3(0,0,0))},"Binary preview");
                var binary=ExternalBlueprintLibrary.Parse(Convert.ToBase64String(binaryPreview),"preview.oprint");
                Require(binary.importError==null && renderer.GetCached(binary) &&
                    BlueprintSourcePresentation.Metadata(binary).Contains("Controlled DTO"),"Binary image/metadata profile not presented");
                provider.Granted=false;
                Require(!renderer.GetCached(binary) && !BlueprintSourcePresentation.Images(binary).Any() &&
                    !BlueprintSourcePresentation.Metadata(binary).Contains("Controlled DTO"),"Binary UGC revoke retained source content");
                provider.Granted=true;
                Require(renderer.GetCached(binary) && binaryModel.Text==binaryOriginal,"Binary grant failed or presentation mutated original");

                var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.SetActive(false);
                string work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/kg319",Guid.NewGuid().ToString("N").Substring(0,8)));
                var store=new CompositeBlueprintStore(Path.Combine(work,"library.json"));
                var item=new BlueprintEditorCatalogItem("blueprint_"+source.id,"Source",renderer.GetCached(source),source.category,"Blueprints","KG",0,source);
                try
                {
                    using(var controller=new BlueprintEditorController(store,template,font,_=>prefab,id=>id,
                        ()=>new[]{item},error=>{throw new InvalidOperationException(error);}))
                    {
                        Require(controller.OpenNew(out string error),error);
                        var view=Field<BlueprintEditorView>(controller,"view");
                        Require(view.ShowImportedCatalog(new[]{item},source),"KG catalog target was not found");yield return null;
                        var body=Field<RectTransform>(view,"catalogPreview").Find("PreviewBody/PreviewBodyContent/PreviewMetadata");
                        Require(body,"KG selected metadata panel missing");
                        var details=body.GetComponent<TMP_Text>();
                        Require(details.text.Contains("＜b＞***＜/b＞"),"KG metadata missing from selected details");
                        provider.Granted=false;view.Tick();yield return null;
                        details=Field<RectTransform>(view,"catalogPreview").Find("PreviewBody/PreviewBodyContent/PreviewMetadata").GetComponent<TMP_Text>();
                        Require(!details.text.Contains("＜b＞") && item.Icon==null,"Visible details/icon not cleared after permission revoke");
                        provider.Granted=true;view.Tick();yield return null;
                    }
                }
                finally{Object.Destroy(prefab);}
                File.WriteAllText(Path.Combine(output,"kg-presentation-acceptance.json"),"{\"passed\":true,\"hostContractsSubstituted\":true,\"nativeGame\":false,\"ugcWarmRevokeGrant\":true,\"binaryDerivedOldImageFixture\":true,\"binarySourceSignGuard\":true,\"literalBoundedMetadata\":true,\"sourceImmutable\":true,\"imageFallback\":true}");
            }
            finally{provider.Granted=true;}
        }
    }
    private static int ForeignHash(string text)
    {unchecked{int a=5381,b=5381;for(int i=0;i<text.Length;i+=2){a=((a<<5)+a)^text[i];if(i==text.Length-1)break;b=((b<<5)+b)^text[i+1];}return a+b*1566083941;}}
    private static byte[] Png(Color color)
    {
        var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try{image.SetPixels(new[]{color,color,color,color});image.Apply();return ImageConversion.EncodeToPNG(image);}
        finally{Object.Destroy(image);}
    }
}

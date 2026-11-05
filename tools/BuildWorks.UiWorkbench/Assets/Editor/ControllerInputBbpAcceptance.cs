using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static partial class ControllerInputAcceptance
{
    internal static IEnumerable<object> RunBbpExports(Camera template, TMP_Text font, string output, bool legacyJson=false)
    {
        string working=Path.GetFullPath(Path.Combine(Application.dataPath,legacyJson?"../../../artifacts/json321":"../../../artifacts/bbp321",Guid.NewGuid().ToString("N")));
        var store=new CompositeBlueprintStore(Path.Combine(working,"native.json"));
        string extension=legacyJson?".json":".bbp";
        string samples=Path.GetFullPath(Path.Combine(Application.dataPath,legacyJson?"../../../artifacts/point-2.16-20261028/producer-run-v2":"../../../artifacts/point-2.15-20261028/producer-run-v1"));
        var original=ExternalBlueprintLibrary.LoadFile(Path.Combine(samples,"qa-snapshot-v3"+extension)).Single();
        var terrain=ExternalBlueprintLibrary.LoadFile(Path.Combine(samples,"controlled-v4-all-terrain2"+extension)).Single();
        Require(original.importError==null && terrain.importError==null,"Original BBP UI fixtures rejected");
        terrain.parts[0].position.x+=1;
        var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.SetActive(false);
        var input=new InputFrames();
        var privileges=Splatform.PlatformManager.DistributionPlatform.PrivilegeProvider;
        bool originalGrant=privileges.Granted;
        try
        {
            privileges.Granted=true;
            byte[] preview=Png(Color.red),originalBytes=original.sourceDocuments.Single().ToGeometry().OriginalBytes();
            var rawSigns=original.ImportCopy();foreach(var p in rawSigns.parts)p.signText=null;rawSigns.importPreview=preview;
            Require(!BlueprintSourcePresentation.Images(rawSigns).Any(),"Cleared native text bypassed raw BBP sign image guard");
            var attachment=ExternalBlueprintLibrary.LoadFile(Path.Combine(samples,"controlled-v2-attachment"+extension)).Single();attachment.importPreview=preview;
            Require(attachment.importError==null && !BlueprintSourcePresentation.Images(attachment).Any(),"BBP display-item source bypassed image guard");
            string PreviewSource(string prefab)
            {
                var rows=new[]{new OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint.Piece(0,prefab,new OstrixMods.BuildWorks.Geometry.Point3(),new OstrixMods.BuildWorks.Geometry.Rotation3(0,0,0,1))};
                return legacyJson ? OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint.WriteJson(1,"Preview",rows) :
                    Convert.ToBase64String(OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint.WriteBinary(1,"Preview",rows));
            }
            var safe=ExternalBlueprintLibrary.Parse(PreviewSource("wood_floor"),"safe"+extension);
            var oldSign=ExternalBlueprintLibrary.Parse(PreviewSource("sign"),"old-sign"+extension);
            oldSign.importPreview=preview;Require(!BlueprintSourcePresentation.Images(oldSign).Any(),"BBP v1 sign prefab without text bypassed source image guard");
            safe.importPreview=preview;Require(BlueprintSourcePresentation.Images(safe).Count()==1,"Safe BBP preview unavailable");
            privileges.Granted=false;Require(!BlueprintSourcePresentation.Images(safe).Any(),"BBP preview ignored UGC revoke");
            privileges.Granted=true;Require(BlueprintSourcePresentation.Images(safe).Count()==1 && original.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(originalBytes),"BBP grant/original immutability failed");
            var uiOriginal=original;
            if(legacyJson)
            {
                string raw=original.sourceDocuments.Single().text;
                uiOriginal=ExternalBlueprintLibrary.Parse(raw.Substring(0,raw.LastIndexOf('}'))+",\"future\":\"<must-not-render>\"}","unknown-source.json");
                Require(uiOriginal.importError==null,"Unknown JSON UI fixture rejected");
                string safeRaw=PreviewSource("wood_floor");
                var unknown=ExternalBlueprintLibrary.Parse(safeRaw.Substring(0,safeRaw.Length-1)+",\"future\":{\"text\":\"hidden\"}}","unknown-preview.json");
                unknown.importPreview=preview;Require(!BlueprintSourcePresentation.Images(unknown).Any(),"Unknown legacy state exposed imported preview");
            }
            using(var controller=new BlueprintEditorController(store,template,font,_=>prefab,id=>id,
                ()=>Array.Empty<BlueprintEditorCatalogItem>(),error=>{throw new InvalidOperationException(error);},editorInput:input))
            {
                Require(controller.OpenNew(out string error),error);var view=Field<BlueprintEditorView>(controller,"view");
                for(int mode=0;mode<4;++mode)
                {
                    bool russian=mode%2==1,blocked=mode>=2;BuildWorksLocalization.RussianLanguage=russian;
                    Require(BlueprintSourcePresentation.Metadata(original)==BuildWorksLocalization.Text(legacyJson?"catalog.bbp_json_metadata":"catalog.bbp_metadata",4,2,0,0,0,"JSON",0),"BBP metadata counts/language differ");
                    Require(BlueprintSourcePresentation.Metadata(terrain)==BuildWorksLocalization.Text(legacyJson?"catalog.bbp_json_metadata":"catalog.bbp_metadata",3,2,1,1,9,"JSON",0),"Nonzero optional metadata counts differ");
                    var source=blocked?terrain:uiOriginal;string name=(legacyJson?"json-":"bbp-")+(blocked?"blocked-":"")+(russian?"ru":"en");
                    view.ShowExchange((parent,textFont,closed)=>new BlueprintExchangeView(parent,textFont,store,source,closed));yield return null;
                    var exchange=Field<BlueprintExchangeView>(view,"exchangeView");
                    for(int i=0;i<600 && Field<Task<string[]>>(exchange,"listing")!=null;++i){Frame(controller,input,Vector2.zero);yield return null;}
                    Field<TMP_InputField>(exchange,"fileName").SetTextWithoutNotify(name);
                    for(int i=0;i<(legacyJson?7:6);++i)NativeClick(view,null,"ExchangeFormat");
                    Require(Field<BetterBuildingExport.Target?>(exchange,"bbpTarget")== (legacyJson?BetterBuildingExport.Target.LegacyJson:BetterBuildingExport.Target.Bbp) && !Field<bool>(exchange,"kgFormat") && !Field<bool>(exchange,"archive") &&
                        !Field<bool>(exchange,"planBuild") && Field<OpenKeepExport.Consumer?>(exchange,"openKeepTarget")==null,"BBP format flags conflict");
                    if(legacyJson && mode==0)
                    {
                        NativeClick(view,null,"ExchangeFormat");Require(Field<bool>(exchange,"plannerFormat"),"Planner option missing after legacy JSON");
                        NativeClick(view,null,"ExchangeFormat");Require(!Field<BetterBuildingExport.Target?>(exchange,"bbpTarget").HasValue && !Field<bool>(exchange,"archive") && !Field<bool>(exchange,"plannerFormat"),"Legacy/planner format cycle did not return to native JSON");
                        for(int i=0;i<7;++i)NativeClick(view,null,"ExchangeFormat");
                        Field<TMP_InputField>(exchange,"fileName").SetTextWithoutNotify("reserved.buildworks");
                        NativeClick(view,null,"ExchangeSubmit");yield return null;
                        Require(Field<TMP_Text>(exchange,"message").text.Contains(BuildWorksLocalization.Text("exchange.bbp_json_reserved_name")) &&
                            !Field<Button>(exchange,"primary").interactable && !Directory.Exists(Path.Combine(store.ExportDirectory,"reserved.buildworks")),"Reserved legacy name accepted by UI");
                        NativeClick(view,null,"ExchangeContentsBack");yield return null;Field<TMP_InputField>(exchange,"fileName").SetTextWithoutNotify(name);
                        Debug.Log("LEGACY_RESERVED_BACK before controller tick: primary="+Field<Button>(exchange,"primary").interactable);
                        Frame(controller,input,Vector2.zero);yield return null;
                        Require(Field<Button>(exchange,"primary").interactable,"Legacy submit did not recover after Back/controller tick");
                    }
                    NativeClick(view,null,"ExchangeSubmit");yield return null;
                    for(int i=0;i<600 && Field<Task<BetterBuildingExport>>(exchange,"bbpInspection")!=null;++i){Frame(controller,input,Vector2.zero);yield return null;}
                    var review=Field<BetterBuildingExport>(exchange,"bbpReview");
                    string title=Field<RectTransform>(exchange,"contentsPanel").Find("ExchangeContentsTitle").GetComponent<TMP_Text>().text;
                    string details=Field<TMP_Text>(exchange,"contentsDetails").text;
                    Require(review!=null && title==BuildWorksLocalization.Text(legacyJson?"exchange.bbp_json_title":"exchange.bbp_title") && details.Contains(name+extension) &&
                        details.Contains(name+".buildworks.json"),"BBP review title/file pair missing; legacy="+legacyJson+" mode="+mode+" target="+Field<BetterBuildingExport.Target?>(exchange,"bbpTarget")+" review="+(review!=null)+" title="+title+" details="+details+" message="+Field<TMP_Text>(exchange,"message").text);
                    if(legacyJson && !blocked)Require(review.UnknownFields==1 && details.Contains(BuildWorksLocalization.Text("exchange.bbp_unknown",1)) && !details.Contains("<must-not-render>"),"Legacy unknown exclusion warning missing or raw text exposed");
                    string destination=Path.Combine(store.ExportDirectory,name);
                    Require(!Directory.Exists(destination),"BBP review wrote before confirmation");
                    if(blocked)
                    {
                        Require(review.Blockers.Any(b=>b.Reason==BetterBuildingExport.BlockReason.TerrainFrame) &&
                            !Field<Button>(exchange,"primary").interactable && Field<Button>(exchange,"contentsBack").interactable,"Stale terrain UI permits export");
                    }
                    else
                    {
                        Require(review.Blockers.Count==0 && review.Signs==2 && Field<Button>(exchange,"primary").interactable,"BBP export review unavailable");
                        NativeClick(view,null,"ExchangeContentsBack");yield return null;Require(!Directory.Exists(destination),"BBP Back wrote files");
                        NativeClick(view,null,"ExchangeSubmit");yield return null;
                        for(int i=0;i<600 && Field<Task<BetterBuildingExport>>(exchange,"bbpInspection")!=null;++i){Frame(controller,input,Vector2.zero);yield return null;}
                        NativeClick(view,null,"ExchangeSubmit");yield return null;
                        for(int i=0;i<600 && Field<Task<string>>(exchange,"planExport")!=null;++i){Frame(controller,input,Vector2.zero);yield return null;}
                        Require(Field<bool>(exchange,"completed") && Directory.GetFiles(destination).Length==2 &&
                            File.Exists(Path.Combine(destination,name+extension)),"BBP UI pair failed: "+Field<TMP_Text>(exchange,"message").text);
                        var full=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(destination,name+".buildworks.json")));
                        Require(full.sourceDocuments.Single().text==source.sourceDocuments.Single().text,"UI paired copy lost exact source");
                    }
                    NativeClick(view,null,"ExchangeClose");yield return null;
                }
                File.WriteAllText(Path.Combine(output,legacyJson?"legacy-json-ui-acceptance.json":"bbp-ui-acceptance.json"),"{\"passed\":true,\"languages\":2,\"originalWriterFixture\":true,\"pairedFiles\":true,\"backNoWrite\":true,\"staleTerrainBlocked\":true,\"rawSignAttachmentImageGuard\":true,\"ugcRevokeGrant\":true,\"sourceImmutable\":true,\"metadataCounts\":true,\"legacyJson\":"+(legacyJson?"true":"false")+",\"reservedName\":"+(legacyJson?"true":"false")+",\"unknownWarningAndArchive\":"+(legacyJson?"true":"false")+",\"nativeGame\":false}");
            }
        }
        finally{privileges.Granted=originalGrant;BuildWorksLocalization.RussianLanguage=false;Object.Destroy(prefab);}
    }
}

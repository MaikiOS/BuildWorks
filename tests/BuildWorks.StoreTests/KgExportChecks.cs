using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class KgExportChecks
    {
        private static CompositeBlueprintStore.VectorData? Unit(string _) =>new CompositeBlueprintStore.VectorData{x=1,y=1,z=1};
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            Require(BlueprintForeignZdo.TryReadKg("AAAAAAAAAA==",out var empty),"Empty KG packet rejected");
            string raw=empty.WriteKg(new[]{new KeyValuePair<int,object>(ForeignZdoChecks.Hash("text"),"original"),
                new KeyValuePair<int,object>(ForeignZdoChecks.Hash("creator"),17L),
                new KeyValuePair<int,object>(ForeignZdoChecks.Hash("ostrix_buildworks_project_operation_v1"),"old-operation"),
                new KeyValuePair<int,object>(ForeignZdoChecks.Hash("ostrix_buildworks_project_receipt_v1"),new byte[]{1,2,3})});
            string text="Name: KG export\nAuthor: original author\nDescription: original description\nBoxRotation: {x: 10, y: 45, z: 20}\nObjects:\n- Id: sign\n  RelativePosition: {x: 1, y: 2, z: 3}\n  Rotation: {x: 90, y: 37}\n  ZDOData: "+raw+"\n- Id: Piece_grausten_floor_1x1\n  RelativePosition: {x: -1}\n";
            var source=ExternalBlueprintLibrary.Parse(text,"source.yml");
            Require(source.importError==null,source.importError);
            var review=KgExport.Review(source,Unit);
            Require(review.Blockers.Count==0 && review.ArchivedFields==2,"Original KG export refused or old project markers retained");
            Require(review.TryWrite(source,Unit,directory,"original",out string result,out string error),error);
            var model=KgBlueprint.Parse(File.ReadAllText(Path.Combine(result,"original.yml")));
            var native=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(result,"original.buildworks.json")));
            Require(model.Author=="original author" && model.Description=="original description" && model.Objects.Count==2 &&
                model.Objects[0].Euler.X==90 && native.sourceDocuments.Single().text==text &&
                native.parts[0].foreignData.zdo==raw,"KG metadata/original Euler/full native source changed");
            Require(BlueprintForeignZdo.TryReadKg(model.Objects[0].ZdoData,out var state) &&
                state.TryGet<string>(ForeignZdoChecks.Hash("text"),out string original) && original=="original" &&
                state.TryGet<long>(ForeignZdoChecks.Hash("creator"),out long creator) && creator==17 &&
                !state.HasHash(ForeignZdoChecks.Hash("ostrix_buildworks_project_operation_v1")) &&
                !state.HasHash(ForeignZdoChecks.Hash("ostrix_buildworks_project_receipt_v1")),"KG state omitted the wrong fields");
            var edited=source.ImportCopy();edited.parts[0].position.x+=1;edited.parts[0].signText="edited";
            var q=KgBlueprintFrame.Euler(new Point3(25,130,-15));
            edited.parts[0].rotation=new CompositeBlueprintStore.QuaternionData{x=(float)q.X,y=(float)q.Y,z=(float)q.Z,w=(float)q.W};
            edited.parts[1].materialSeed=1234;
            foreach(string culture in new[]{"en-US","ru-RU"})
            {
                CultureInfo before=CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
                    var changed=KgExport.Review(edited,Unit);
                    Require(changed.Blockers.Count==0,"Edited KG export blocked");
                    Require(changed.TryWrite(edited,Unit,directory,culture,out string path,out error),error);
                    var loaded=ExternalBlueprintLibrary.LoadFile(Path.Combine(path,culture+".yml")).Single();
                    var ready=ExternalBlueprintLibrary.PrepareForHost(loaded,_=>null,ForeignZdoChecks.Hash,
                        supportsMaterialSeed:_=>true,supportsKgState:_=>true);
                    Require(ready.importError==null && ready.parts[0].signText=="edited" && ready.parts[1].materialSeed==1234 &&
                        Math.Abs(ready.parts[0].position.x-edited.parts[0].position.x)<.0001 &&
                        source.parts[0].foreignData.zdo==raw,"Edited KG pose/state/culture/source immutability lost");
                }
                finally{CultureInfo.CurrentCulture=before;}
            }
            var independent=edited.ImportCopy();independent.sourceDocuments.Clear();
            foreach(var part in independent.parts){part.sourceDocumentId=null;part.sourceRecord=0;part.sourceFrameApplied=false;part.foreignData=null;}
            var fresh=KgExport.Review(independent,Unit);
            Require(fresh.Blockers.Count==0,"No-source KG export blocked");
            Require(fresh.TryWrite(independent,Unit,directory,"fresh",out string freshPath,out error),error);
            var newModel=KgBlueprint.Parse(File.ReadAllText(Path.Combine(freshPath,"fresh.yml")));
            Require(newModel.Objects.Count==2 && newModel.Objects.All(piece=>piece.Id!="template") && newModel.Author=="BuildWorks",
                "No-KG export leaked template row or fake original author");
            var scaled=source.ImportCopy();scaled.parts[0].scale.x=2;
            Require(KgExport.Review(scaled,Unit).Blockers.Any(item=>item.Reason==KgExport.BlockReason.Scale),"KG unrepresentable scale silently changed");
            var otherState=source.ImportCopy();otherState.parts[0].foreignData=new CompositeBlueprintStore.ForeignPartData{
                format=BlueprintForeignPartData.NativeItemStandFormat,info="AxeIron:0:1:0"};
            Require(KgExport.Review(otherState,Unit).Blockers.Any(item=>item.Reason==KgExport.BlockReason.StateAdapter),"KG unsupported state silently omitted");
            var multiple=source.ImportCopy();multiple.sourceDocuments.Add(CompositeBlueprintStore.SourceDocument.FromGeometry(
                new BlueprintSourceDocument(KgBlueprint.Format,"other.yml",text.Replace("Name: KG export","Name: other"))));
            Require(KgExport.Review(multiple,Unit).Blockers.Any(item=>item.Reason==KgExport.BlockReason.MultipleSources),"KG metadata roots silently merged");
            var oldRoof=ExternalBlueprintLibrary.Parse("Name: roof\nObjects: [{Id: wood_wall_roof}]\n","roof.yml");
            var migrated=ExternalBlueprintLibrary.MigrateLegacyRoof(oldRoof,true);
            var migration=KgExport.Review(migrated,Unit);
            Require(migration.Blockers.Count==0 && migrated.parts[0].originalPrefabName==BlueprintPrefabMigration.OldRoof,"Recorded KG roof migration rejected");
            Require(migration.TryWrite(migrated,Unit,directory,"roof",out string roofPath,out error),error);
            Require(KgBlueprint.Parse(File.ReadAllText(Path.Combine(roofPath,"roof.yml"))).Objects.Single().Id==BlueprintPrefabMigration.CurrentRoof,
                "KG export resurrected obsolete roof Id");
            var stale=KgExport.Review(source,Unit);var moved=source.ImportCopy();moved.parts[0].position.y+=1;
            Require(!stale.TryWrite(moved,Unit,directory,"stale",out _,out error) && error=="$buildworks_exchange.plan_changed" &&
                !Directory.Exists(Path.Combine(directory,"stale")),"KG stale plan published");
            var cancelled=KgExport.Review(source,Unit);cancelled.CancelWrite();bool stopped=false;
            try{cancelled.TryWrite(source,Unit,directory,"cancel",out _,out _);}catch(OperationCanceledException){stopped=true;}
            Require(stopped && !Directory.Exists(Path.Combine(directory,"cancel")) && !Directory.EnumerateDirectories(directory,"*.tmp").Any(),
                "Cancelled KG pair left files");
            int main=System.Threading.Thread.CurrentThread.ManagedThreadId;
            var store=new CompositeBlueprintStore(Path.Combine(directory,"store","library.json"));
            store.ConfigureImportScale(name=>{
                Require(System.Threading.Thread.CurrentThread.ManagedThreadId==main,"KG export invoked host scale on worker");
                return Unit(name);
            });
            var storeSource=source.ImportCopy();
            storeSource.parts[0].scaleIsAbsolute=true;
            var storeReview=store.ReviewKgExport(storeSource).GetAwaiter().GetResult();
            Require(storeReview.Blockers.Count==0,"KG Store review failed");
            string storePath=store.ExportKg(storeReview,storeSource,directory,"store-export").GetAwaiter().GetResult();
            Require(File.Exists(Path.Combine(storePath,"store-export.yml")) &&
                File.Exists(Path.Combine(storePath,"store-export.buildworks.json")),"KG Store did not write both files");
            Console.WriteLine("PASS KG paired export: source metadata/frame/typed state, edited text/seed, en+ru, no-source/migration, explicit blockers, stale/cancel cleanup; no native consumer proof");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static void Actual(string file,string directory)
        {
            var before=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ru-RU");
                var source=ExternalBlueprintLibrary.LoadFile(file).Single();
                var edited=ExternalBlueprintLibrary.PrepareForHost(source,_=>null,ForeignZdoChecks.Hash,
                    supportsMaterialSeed:name=>name=="Piece_grausten_floor_1x1",
                    resolvePrefabHash:id=>id==706748652?"Piece_grausten_floor_1x1":null,
                    supportsKgState:name=>name=="sign" || name=="Piece_grausten_floor_1x1");
                Require(edited.importError==null && edited.parts.Count==4,"Actual KG export input differs");
                edited.parts[0].position.x+=.25f;edited.parts[0].materialSeed=17;edited.parts[2].signText="BuildWorks KG edited";
                var q=KgBlueprintFrame.Euler(new Point3(25,130,-15));
                edited.parts[0].rotation=new CompositeBlueprintStore.QuaternionData{x=(float)q.X,y=(float)q.Y,z=(float)q.Z,w=(float)q.W};
                var review=KgExport.Review(edited,Unit);
                Require(review.Blockers.Count==0 && review.ArchivedFields>0,"Actual KG export blocked or lifecycle fields not reported");
                Require(review.TryWrite(edited,Unit,directory,"edited",out string path,out string error),error);
                var reloaded=ExternalBlueprintLibrary.LoadFile(Path.Combine(path,"edited.yml")).Single();
                var ready=ExternalBlueprintLibrary.PrepareForHost(reloaded,_=>null,ForeignZdoChecks.Hash,
                    supportsMaterialSeed:name=>name=="Piece_grausten_floor_1x1",
                    resolvePrefabHash:id=>id==706748652?"Piece_grausten_floor_1x1":null,supportsKgState:_=>true);
                var native=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(path,"edited.buildworks.json")));
                Require(ready.importError==null && ready.parts[0].materialSeed==17 && ready.parts[2].signText=="BuildWorks KG edited" &&
                    native.sourceDocuments.Single().text==source.sourceDocuments.Single().text,"Actual edited KG cycle lost state/source");
                var output=KgBlueprint.Parse(File.ReadAllText(Path.Combine(path,"edited.yml")));
                var expected=new ReaderModel{Name=output.Name,Author=output.Author,Description=output.Description,
                    IconSHA256=ImageHash(output.Icon),PreviewSHA256=output.HasPreviewList?output.Previews.Select(ImageHash).ToArray():null,
                    BoxRotation=Vector(output.BoxRotation),Objects=output.Objects.Select(piece=>new ReaderObject{
                        IdString=piece.PrefabName,IdInt=piece.PrefabHash ?? ForeignZdoChecks.Hash(piece.PrefabName),Prefab=piece.Prefab,Position=Vector(piece.Position),
                        Rotation=Vector(piece.Euler),ZDOData=piece.ZdoData}).ToArray()};
                using(var stream=File.Create(Path.Combine(path,"expected-reader.json")))
                    new DataContractJsonSerializer(typeof(ReaderModel)).WriteObject(stream,expected);
                Console.WriteLine("PASS actual edited KG backend pair: "+path+"; original consumer still requires separate proof");
            }
            finally{CultureInfo.CurrentCulture=before;}
        }
        private static float[] Vector(Point3 p)=>new[]{(float)p.X,(float)p.Y,(float)p.Z};
        private static string ImageHash(string text)
        {if(text==null)return null;using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Convert.FromBase64String(text))).Replace("-","");}
        [DataContract] private sealed class ReaderModel
        {
            [DataMember] public string Name,Author,Description,IconSHA256;
            [DataMember] public string[] PreviewSHA256;
            [DataMember] public float[] BoxRotation;
            [DataMember] public ReaderObject[] Objects;
        }
        [DataContract] private sealed class ReaderObject
        {
            [DataMember] public string IdString,Prefab,ZDOData;
            [DataMember] public int? IdInt;
            [DataMember] public float[] Position,Rotation;
        }
    }
}

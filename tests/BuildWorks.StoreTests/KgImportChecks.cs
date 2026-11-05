using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class KgImportChecks
    {
        private const string Simple="Name: KG own\nBoxRotation: {y: 90}\nObjects:\n- Id: sign\n  RelativePosition: {x: 2, y: 1}\n  Rotation: {y: 180}\n- Id: '706748652'\n  Prefab: wrong_hint\n  RelativePosition: {z: 2}\n";
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var parsed=ExternalBlueprintLibrary.Parse(Simple,"own.yml");
            Require(parsed.importError==null && parsed.parts.Count==2,"KG dispatch: "+parsed.importError);
            Require(parsed.parts[0].position.z>1.9999f && Math.Abs(parsed.parts[0].position.x)<1e-5 && parsed.parts[0].position.y==1,
                "Inverse BoxRotation was not applied to position");
            Require(parsed.parts.All(p=>p.sourceLine==0 && p.sourceFrameApplied && !p.scaleIsAbsolute) &&
                parsed.parts[1].prefabName=="kg_hash_706748652" && CompositeBlueprintStore.ValidSources(parsed.parts,parsed.sourceDocuments),
                "Hash Id became public Prefab hint,absolute scale,or invalid binding");
            var prepared=ExternalBlueprintLibrary.PrepareForHost(parsed,_=>null,ForeignZdoChecks.Hash,
                resolvePrefabHash:_=>"Piece_grausten_floor_1x1");
            Require(prepared.importError==null && prepared.parts[1].prefabName=="Piece_grausten_floor_1x1" &&
                parsed.parts[1].prefabName=="kg_hash_706748652" && CompositeBlueprintStore.ValidSources(prepared.parts,prepared.sourceDocuments),
                "Host hash resolution/binding or input immutability lost");
            var absent=ExternalBlueprintLibrary.PrepareForHost(parsed,_=>null,ForeignZdoChecks.Hash,resolvePrefabHash:_=>null);
            Require(absent.parts[1].prefabName==parsed.parts[1].prefabName,"Unavailable hash guessed a prefab");
            var wrong=ExternalBlueprintLibrary.PrepareForHost(parsed,_=>null,ForeignZdoChecks.Hash,resolvePrefabHash:_=>"woodwall");
            Require(wrong.importError!=null && wrong.parts.Count==0,"Wrong native hash mapping exposed partial import");
            Frame();
            KnownState(directory);
            HostPipeline(Path.Combine(directory,"host-pipeline"));
            RoundTrip(prepared,Path.Combine(directory,"roundtrip"));
            var mixed=Path.Combine(directory,"mixed.zip");
            using(var zip=ZipFile.Open(mixed,ZipArchiveMode.Create))
            {
                using(var writer=new StreamWriter(zip.CreateEntry("own.yml").Open()))writer.Write(Simple);
                using(var writer=new StreamWriter(zip.CreateEntry("legacy.blueprint").Open()))writer.Write("#Pieces\nwood_floor;;0;0;0;0;0;0;1;\"\"\n");
            }
            var found=ExternalBlueprintLibrary.LoadFile(mixed);
            Require(found.Count==2 && found.All(p=>p.importError==null),"KG ZIP discovery lost supported entries");
            Require(ExternalBlueprintLibrary.Parse("enabled: true","config.yml").importError!=null,"Generic YAML config became a blueprint");
            Console.WriteLine("PASS KG dispatch/hash resolution/inverse frame/ZIP/raw/native31portable19 and bound/unbound old-version refusal;not native-game/math proof");
        }
        internal static void Actual(string file,string directory)
        {
            byte[] before=File.ReadAllBytes(file);
            var source=ExternalBlueprintLibrary.LoadFile(file).Single();
            Require(source.importError==null && source.parts.Count==4,"Actual KG import: "+source.importError);
            var prepared=ExternalBlueprintLibrary.PrepareForHost(source,_=>null,ForeignZdoChecks.Hash,0,
                supportsMaterialSeed:name=>name=="Piece_grausten_floor_1x1",resolvePrefabHash:id=>id==706748652?"Piece_grausten_floor_1x1":null,
                supportsKgState:name=>name=="sign" || name=="Piece_grausten_floor_1x1");
            Require(prepared.importError==null && prepared.parts[0].materialSeed==0 && prepared.parts[1].materialSeed==1234 &&
                prepared.parts[2].signText=="BuildWorks QA 2.7" && prepared.parts[3].signText=="" &&
                prepared.parts.All(p=>p.foreignData==null),
                "Known KG sign/material projection failed");
            var archive=KgBlueprint.Parse(prepared.sourceDocuments.Single().text);
            Require(prepared.sourceDocuments.Single().text==source.sourceDocuments.Single().text &&
                source.parts.Select((p,i)=>p.foreignData.zdo==archive.Objects[i].ZdoData).All(p=>p),"KG full source ZDO bytes changed");
            RoundTrip(prepared,directory);
            Require(File.ReadAllBytes(file).SequenceEqual(before),"Actual KG original bytes changed");
            Console.WriteLine("PASS actual4-piece KG full-source/ZDO/meta/native31portable19 cycle;known text/seed projection;not full2.13");
        }
        private static void KnownState(string directory)
        {
            string Packet(int? seed=null,string text=null,string extra=null,bool wrongSeed=false,bool wrongAuthor=false,bool wrongCreator=false,bool provenance=false)
            {
                using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream,Encoding.UTF8))
                {
                    writer.Write(false);writer.Write(false);writer.Write(false);
                    writer.Write(seed.HasValue || wrongCreator);
                    if(seed.HasValue || wrongCreator)
                    {
                        writer.Write((byte)((seed.HasValue?1:0)+(wrongCreator?1:0)));
                        if(seed.HasValue){writer.Write(ForeignZdoChecks.Hash("RandMatSeed"));writer.Write(seed.Value);}
                        if(wrongCreator){writer.Write(ForeignZdoChecks.Hash("creator"));writer.Write(1);}
                    }
                    writer.Write(provenance);
                    if(provenance){writer.Write((byte)1);writer.Write(ForeignZdoChecks.Hash("creator"));writer.Write(1L);}
                    int strings=(text!=null?1:0)+(extra!=null?1:0)+(wrongSeed?1:0);
                    writer.Write(strings>0);
                    if(strings>0)
                    {
                        writer.Write((byte)strings);
                        if(text!=null){writer.Write(ForeignZdoChecks.Hash("text"));writer.Write(text);}
                        if(extra!=null){writer.Write(ForeignZdoChecks.Hash(extra));writer.Write("unknown");}
                        if(wrongSeed){writer.Write(ForeignZdoChecks.Hash("RandMatSeed"));writer.Write("1234");}
                    }
                    writer.Write(wrongAuthor);
                    if(wrongAuthor){writer.Write((byte)1);writer.Write(ForeignZdoChecks.Hash("author"));writer.Write(1);writer.Write((byte)0);}
                    return Convert.ToBase64String(stream.ToArray());
                }
            }
            CompositeBlueprintStore.Blueprint Parse(string packet,string prefab="sign")=>ExternalBlueprintLibrary.Parse(
                "Name: state\nObjects:\n- Id: "+prefab+"\n  ZDOData: "+packet+"\n","state.yml");
            CompositeBlueprintStore.Blueprint Prepare(CompositeBlueprintStore.Blueprint input,bool capable=true)=>
                ExternalBlueprintLibrary.PrepareForHost(input,_=>null,ForeignZdoChecks.Hash,
                    supportsMaterialSeed:_=>capable,supportsKgState:_=>true);
            foreach(var raw in new[]{Parse(Packet(text:"original")),Parse(Packet(seed:1234),"Piece_grausten_floor_1x1")})
            {
                string exact=raw.sourceDocuments.Single().text;
                raw.parts[0].signText=raw.parts[0].prefabName=="sign"?"already edited":null;
                raw.parts[0].materialSeed=raw.parts[0].prefabName=="sign"?(int?)null:17;
                var projected=Prepare(raw);
                Require(projected.parts[0].foreignData==null && raw.parts[0].foreignData!=null &&
                    projected.sourceDocuments.Single().text==exact && projected.parts[0].signText==raw.parts[0].signText &&
                    projected.parts[0].materialSeed==raw.parts[0].materialSeed,"Known projection mutated raw source or prior edits");
                projected.parts[0].signText=projected.parts[0].prefabName=="sign"?"edited":null;
                projected.parts[0].materialSeed=projected.parts[0].prefabName=="sign"?(int?)null:9;
                var again=Prepare(projected);
                Require(again.parts[0].signText==projected.parts[0].signText && again.parts[0].materialSeed==projected.parts[0].materialSeed,
                    "Repreparation overwrote edited text/seed");
                RoundTrip(again,Path.Combine(directory,"edited-"+projected.parts[0].prefabName));
            }
            foreach(string packet in new[]{Packet(text:"original",extra:"future"),Packet(text:"original",wrongAuthor:true),
                Packet(text:"original",wrongCreator:true),Packet(seed:-1),Packet(wrongSeed:true),Packet(seed:1234,extra:"future")})
            {
                var raw=Parse(packet);var rejected=Prepare(raw);
                Require(rejected.importError==null && rejected.parts[0].foreignData?.zdo==packet &&
                    rejected.parts[0].signText==null && rejected.parts[0].materialSeed==null,
                    "Unknown/wrong-typed KG state lost blocker or leaked partial projection");
            }
            var unsupported=Parse(Packet(seed:1234),"Piece_grausten_floor_1x1");
            Require(Prepare(unsupported,false).parts[0].foreignData!=null,"Unsupported material capability accepted");
            var rebound=Parse(Packet(text:"original"));rebound.parts[0].foreignData.zdo=Packet(text:"rebound");
            Require(Prepare(rebound).parts[0].foreignData!=null,"Unbound KG payload was trusted");
            foreach(var packet in new[]{Packet(),Packet(provenance:true)})
            {
                var special=Parse(packet,"kg_review_container");
                var blocked=ExternalBlueprintLibrary.PrepareForHost(special,_=>null,ForeignZdoChecks.Hash,supportsKgState:_=>false);
                Require(blocked.parts[0].foreignData?.zdo==packet && CompositeBlueprintStore.InspectContents(blocked,ForeignZdoChecks.Hash).Unknown>0,
                    "Empty/provenance package bypassed special-state guard");
                Require(ExternalBlueprintLibrary.PrepareForHost(special,_=>null,ForeignZdoChecks.Hash).parts[0].foreignData!=null,
                    "Unknown host capability dropped raw state");
            }
            var noState=ExternalBlueprintLibrary.Parse("Name: missing state\nObjects:\n- Id: kg_review_container\n","missing.yml");
            var refused=ExternalBlueprintLibrary.PrepareForHost(noState,_=>null,ForeignZdoChecks.Hash,supportsKgState:_=>false);
            Require(refused.importError!=null && refused.parts.Count==0 && noState.parts.Count==1,
                "Absent special state became an invented empty container");
            Console.WriteLine("PASS KG known projection/raw-source preservation/edited reopen/unknown+wrong-type atomic refusal/capability/binding");
        }
        private static void HostPipeline(string directory)
        {
            Directory.CreateDirectory(directory);
            const string prefab="Piece_grausten_floor_1x1";
            int main=System.Threading.Thread.CurrentThread.ManagedThreadId;
            void OnMain()=>Require(System.Threading.Thread.CurrentThread.ManagedThreadId==main,"KG worker invoked native resolver");
            var store=new CompositeBlueprintStore(Path.Combine(directory,"library.json"),backgroundImports:true);
            store.ConfigureImportScale(name=>{OnMain();return name==prefab?(CompositeBlueprintStore.VectorData?)
                new CompositeBlueprintStore.VectorData{x=1,y=1,z=1}:null;},prefabHash:ForeignZdoChecks.Hash,
                supportsMaterialSeed:name=>{OnMain();return name==prefab;},
                containerGrid:name=>{OnMain();return null;},
                resolvePrefabHash:id=>{OnMain();return id==706748652?prefab:null;},
                supportsKgState:name=>{OnMain();return name==prefab?(bool?)true:null;});
            Wait();
            string path=Path.Combine(directory,"hash.yml");
            File.WriteAllText(path,"Name: numeric pipeline\nObjects:\n- Id: 706748652\n");
            var raw=CompositeBlueprintStore.ReviewImportFile(path,ForeignZdoChecks.Hash);
            var review=store.PrepareImportReview(raw,ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(review.Missing.Known && review.Missing.Parts==0 && review.Documents.Single().parts.Single().prefabName==prefab,
                "Numeric KG Id was incorrectly reported missing");
            Require(store.TryImportReviewed(raw,false,false,ForeignZdoChecks.Hash,out string error),error);
            Wait();
            Require(store.ImportError==null && store.CatalogAll().Single().parts.Single().prefabName==prefab,
                "Raw numeric KG import/catalog failed: "+store.ImportError);
            string mixed=Path.Combine(directory,"mixed.yml");
            File.WriteAllText(mixed,"Name: numeric and missing\nObjects:\n- Id: 706748652\n- Id: kg_review_missing\n");
            var mixedRaw=CompositeBlueprintStore.ReviewImportFile(mixed,ForeignZdoChecks.Hash);
            var missing=store.PrepareImportReview(mixedRaw,ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(missing.Missing.CanOmit && missing.Missing.Parts==1,"KG missing summary counted a resolved hash");
            Require(store.TryExportFile(mixedRaw.Documents.Single(),Path.Combine(directory,"exports"),"kept",false,
                out string exported,out error,prefabHash:ForeignZdoChecks.Hash,omitMissing:missing.Missing),error);
            Require(BlueprintExchangeFormat.Decode(File.ReadAllText(exported)).parts.Single().prefabName==prefab,
                "Native export removed the present numeric prefab");
            string catalog=Path.Combine(store.ImportDirectory,"hash.yml");File.WriteAllText(catalog,File.ReadAllText(path));
            Require(store.RetryImportCatalog(store.ImportResultPath,out error),error);Wait();
            Require(store.CatalogAll().All(document=>document.parts.Single().prefabName==prefab),"Background catalog left a hash sentinel");
            Console.WriteLine("PASS KG real Store review/import/catalog/missing/export paths; native callbacks stay on main thread");
            void Wait()=>Require(System.Threading.SpinWait.SpinUntil(()=>{store.CatalogAll();return !store.CatalogLoading;},10000),"KG Store worker timeout");
        }
        private static void RoundTrip(CompositeBlueprintStore.Blueprint source,string directory)
        {
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"library.json");
            var store=new CompositeBlueprintStore(path);
            Require(store.TryGetOrCreatePlacementCopy(source,out var saved,out string error),error);
            byte[] bytes=BlueprintExchangeFormat.Encode(saved);
            var decoded=BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(bytes));
            var reopened=new CompositeBlueprintStore(path).All().Single();
            Require(bytes.SequenceEqual(BlueprintExchangeFormat.Encode(decoded)) && bytes.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)),
                "KG native/portable fields differ");
            Require(reopened.sourceDocuments.Single().text==source.sourceDocuments.Single().text &&
                reopened.parts.Select((p,i)=>p.sourceRecord==i+1 && p.sourceFrameApplied).All(p=>p),"KG raw/records/frame lost");
            string portable=Encoding.UTF8.GetString(bytes);
            Refuse(portable.Replace("\"version\":"+BlueprintExchangeFormat.Version,"\"version\":18"));
            var orphan=decoded.ImportCopy();
            foreach(var part in orphan.parts){part.sourceDocumentId=null;part.sourceRecord=0;part.sourceFrameApplied=false;part.foreignData=null;}
            string orphanJson=Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(orphan));
            Refuse(orphanJson.Replace("\"version\":"+BlueprintExchangeFormat.Version,"\"version\":18"));
            string oldNative=File.ReadAllText(path).Replace("\"version\":"+CompositeBlueprintStore.FormatVersion,"\"version\":30");
            string oldPath=Path.Combine(directory,"old.json");File.WriteAllText(oldPath,oldNative);
            var blocked=new CompositeBlueprintStore(oldPath);
            Require(blocked.All().Count==0 && !blocked.TryAddCategory("Blocked",out _) && File.ReadAllText(oldPath)==oldNative,
                "KG new profile entered or overwrote native30");
            var invalid=CompositeBlueprintStore.CloneParts(saved.parts);invalid[0].sourceRecord=source.parts.Count+1;
            Require(!CompositeBlueprintStore.ValidSources(invalid,saved.sourceDocuments),"Bad KG ordinal accepted");
            invalid=CompositeBlueprintStore.CloneParts(saved.parts);invalid[0].prefabName="wrong";
            Require(!CompositeBlueprintStore.ValidSources(invalid,saved.sourceDocuments),"Wrong KG prefab binding accepted");
        }
        private static void Frame()
        {
            var box=new Point3(10,45,20);
            var piece=new KgBlueprint.Piece(1,"sign",new Point3(1,2,-3),new Point3(25,130,-15));
            var pose=KgBlueprintFrame.Canonical(piece,box);
            Near(Rotate(box,pose.Position),piece.Position);
            foreach(var axis in new[]{new Point3(1,0,0),new Point3(0,1,0),new Point3(0,0,1)})
                Near(Rotate(box,pose.Rotation.Rotate(axis)),Rotate(piece.Euler,axis));
        }
        // Independent Z->X->Y axis matrix reference, not the production quaternion formula.
        private static Point3 Rotate(Point3 angles,Point3 p)
        {
            double x=angles.X*Math.PI/180,y=angles.Y*Math.PI/180,z=angles.Z*Math.PI/180;
            var a=new Point3(p.X*Math.Cos(z)-p.Y*Math.Sin(z),p.X*Math.Sin(z)+p.Y*Math.Cos(z),p.Z);
            var b=new Point3(a.X,a.Y*Math.Cos(x)-a.Z*Math.Sin(x),a.Y*Math.Sin(x)+a.Z*Math.Cos(x));
            return new Point3(b.X*Math.Cos(y)+b.Z*Math.Sin(y),b.Y,-b.X*Math.Sin(y)+b.Z*Math.Cos(y));
        }
        private static void Near(Point3 a,Point3 b)=>Require(Math.Abs(a.X-b.X)<1e-6 && Math.Abs(a.Y-b.Y)<1e-6 && Math.Abs(a.Z-b.Z)<1e-6,"KG Euler frame order changed");
        private static void Refuse(string text)
        {try{BlueprintExchangeFormat.Decode(text);}catch(InvalidDataException){return;}throw new InvalidOperationException("KG bypassed portable18 profile gate");}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}

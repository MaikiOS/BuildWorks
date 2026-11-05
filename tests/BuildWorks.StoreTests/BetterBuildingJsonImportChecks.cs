using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using Bbp = OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class BetterBuildingJsonImportChecks
    {
        private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        private static CompositeBlueprintStore.VectorData? Basis(string _) => new CompositeBlueprintStore.VectorData { x=1,y=1,z=1 };
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var signedZero = Bbp.ParseJson("{\"pieces\":[{\"prefab\":\"wood_floor\",\"x\":-0.0,\"qx\":\" -0.0 \"}],\"terrain\":{\"width\":2,\"radius\":1,\"originX\":-0.0,\"heights\":[-0.0,0,0,0]}}");
            Check(new[] { (float)signedZero.Pieces[0].Position.X, (float)signedZero.Pieces[0].Rotation.X, (float)signedZero.Terrain.OriginX, signedZero.Terrain.Heights[0] }
                .All(v => v == 0 && float.IsNegativeInfinity(1 / v)), "Legacy signed-zero parse lost float sign on runtime framework");
            Check(Bbp.WriteJson(1, signedZero.Name, signedZero.Pieces, signedZero.Terrain).Contains("\"heights\":[-0.0,0,0,0]"), "Legacy terrain signed-zero export lost float sign");
            var rows = new[] { new Bbp.Piece(1,"itemstand",new Point3(2,3,4),KgBlueprintFrame.Euler(new Point3(15,40,25)),
                displayItem:new Bbp.Attachment("SwordIron",7,3)),
                new Bbp.Piece(2,"sign",new Point3(-1,2,5),new Rotation3(0,0,0,1),"",scale:new Point3(1.5,.5,2)) };
            string json=Bbp.WriteJson(4,"legacy",rows);
            json=json.Substring(0,json.Length-1)+",\"future\":{\"value\":\"<private>\"}}";
            string input=Path.Combine(directory,"input.json"); File.WriteAllText(input,json,new UTF8Encoding(false,true));
            var source=ExternalBlueprintLibrary.LoadFile(input).Single();
            Check(source.importError==null && source.parts.Count==2,"Legacy file dispatch: "+source.importError);
            Check(source.sourceDocuments.Single().format==Bbp.JsonFormat && source.parts.All(p=>p.sourceFrameApplied && !p.scaleIsAbsolute) &&
                source.parts[1].sourceRecord==2 && source.parts[1].signText=="" && source.parts[1].scale.x==1.5f,"Legacy frame/sign/scale/record");
            byte[] original=new UTF8Encoding(false,true).GetBytes(json);
            Check(source.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original),"Legacy original is not exact JSON");
            long budget=ExternalBlueprintLibrary.MaximumTotalBytes;
            Check(ExternalBlueprintLibrary.BrowsableFile(input,ref budget),"Legacy file hidden from browser");
            string openKeep="{\"formatVersion\":1,\"pieces\":[[\"wood_floor\",0,0,0,0]]}";
            Check(ExternalBlueprintLibrary.Parse(openKeep,"openkeep.json").sourceDocuments.Single().format==OpenKeepBlueprint.Format,"OpenKeep route changed");
            string zipPath=Path.Combine(directory,"mixed.zip");
            using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create))
            {
                using(var w=new StreamWriter(zip.CreateEntry("nested/input.json").Open(),new UTF8Encoding(false,true)))w.Write(json);
                using(var w=new StreamWriter(zip.CreateEntry("openkeep.json").Open(),new UTF8Encoding(false,true)))w.Write(openKeep);
            }
            var zipped=ExternalBlueprintLibrary.LoadFile(zipPath).ToArray();
            Check(zipped.Length==2 && zipped.All(b=>b.importError==null) && zipped.Count(b=>b.sourceDocuments.Single().format==Bbp.JsonFormat)==1,"Mixed JSON ZIP routes");
            string upper=Path.Combine(directory,"upper.json");File.WriteAllText(upper,"{\"FORMATVERSION\":1,\"PIECES\":[{\"PREFAB\":\"wood_floor\"}]}");
            Check(ExternalBlueprintLibrary.LoadFile(upper).Single().importError==null,"Uppercase legacy admission lost");
            Check(ExternalBlueprintLibrary.Parse("{", "bad.json").importError!=null,"Malformed direct JSON escaped or succeeded");
            var ambiguous=ExternalBlueprintLibrary.Parse("{\"pieces\":[{\"prefab\":\"wood_floor\",\"position\":{\"x\":100,\"y\":2,\"z\":3}}]}","other.json");
            Check(ambiguous.importError!=null && ambiguous.parts.Count==0,"Other object schema became zero-position legacy parts");
            RoundTrip(source,Path.Combine(directory,"roundtrip"));
            var review=BetterBuildingExport.Review(source,Basis,BetterBuildingExport.Target.LegacyJson);
            Check(review.Blockers.Count==0 && review.UnknownFields==1 && review.Attachments==1 && review.Scales==1,"Legacy review counts");
            Check(!review.TryWrite(source,Basis,directory,"house.buildworks",out _,out string error) &&
                error=="$buildworks_exchange.bbp_json_reserved_name" && !Directory.Exists(Path.Combine(directory,"house.buildworks")),"Reserved native suffix exported");
            Check(review.TryWrite(source,Basis,directory,"pair",out string pair,out error),error);
            var foreign=Bbp.ParseJson(File.ReadAllText(Path.Combine(pair,"pair.json")));
            var complete=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(pair,"pair.buildworks.json")));
            Check(foreign.Pieces.Count==2 && foreign.Pieces[0].DisplayItem.Orientation==3 && foreign.UnknownPaths.Count==0 &&
                complete.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original),"Legacy target/full copy/unknown archive");
            Check(ExternalBlueprintLibrary.LoadFile(Path.Combine(pair,"pair.json")).Single().sourceDocuments.Single().format==Bbp.JsonFormat,"Exported filename dispatched as another profile");
            var edited=source.ImportCopy();edited.parts.Reverse();edited.parts[0].position.x+=.25f;edited.parts[0].signText="edited";
            var changed=BetterBuildingExport.Review(edited,Basis,BetterBuildingExport.Target.LegacyJson);
            Check(changed.TryWrite(edited,Basis,directory,"edited",out pair,out error),error);
            foreign=Bbp.ParseJson(File.ReadAllText(Path.Combine(pair,"edited.json")));
            Check(foreign.Pieces[0].SignText=="edited" && foreign.Pieces[0].Position.X==-.75 && foreign.Pieces[1].DisplayItem.Variant==7,"Edited/reordered JSON fields lost");
            var bbp=BetterBuildingExport.Review(source,Basis);
            Check(bbp.TryWrite(source,Basis,directory,"binary",out pair,out error),error);
            Check(Bbp.ParseBinary(File.ReadAllBytes(Path.Combine(pair,"binary.bbp"))).Pieces[0].DisplayItem.Prefab=="SwordIron","JSON to BBPF loses optional source data");
            var moved=source.ImportCopy();moved.parts[0].position.y+=1;
            Check(!review.TryWrite(moved,Basis,directory,"stale",out _,out _) && !Directory.Exists(Path.Combine(directory,"stale")),"Stale JSON review published");
            var cancelled=BetterBuildingExport.Review(source,Basis,BetterBuildingExport.Target.LegacyJson);cancelled.CancelWrite();bool stopped=false;
            try{cancelled.TryWrite(source,Basis,directory,"cancelled",out _,out _);}catch(OperationCanceledException){stopped=true;}
            Check(stopped && !Directory.Exists(Path.Combine(directory,"cancelled")),"Cancelled legacy pair published");
            Check(!review.TryWrite(source,Basis,directory,"pair",out _,out _) && File.ReadAllText(input)==json,"Existing pair/source overwritten");
            CheckBudgets(source,directory);
            Console.WriteLine("PASS legacy JSON file/browser/mixedZIP/raw/native34portable22/source-only downgrade/edited pair/target/reserved name/cancel/stale; no game proof");
        }
        private static void CheckBudgets(CompositeBlueprintStore.Blueprint source,string directory)
        {
            var malicious=BlueprintExportPair.Snapshot(source).Document;
            foreach(var p in malicious.parts){p.sourceDocumentId=null;p.sourceRecord=0;p.sourceFrameApplied=false;}
            // Invalid raw JSON and IDs must not bypass the aggregate pre-parse quota.
            string text=new string('x',6*1024*1024+1);
            malicious.sourceDocuments=Enumerable.Range(0,4).Select(i=>new CompositeBlueprintStore.SourceDocument {
                id="invalid"+i,format=Bbp.JsonFormat,name=i+".json",text=text }).ToList();
            var type=typeof(BlueprintExchangeFormat).GetNestedType("Envelope",BindingFlags.NonPublic);
            object envelope=Activator.CreateInstance(type,true);
            type.GetField("format").SetValue(envelope,"buildworks-blueprint");type.GetField("version").SetValue(envelope,BlueprintExchangeFormat.Version);
            type.GetField("requiredPrefabIds").SetValue(envelope,malicious.parts.Select(p=>p.prefabName).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToList());
            type.GetField("blueprint").SetValue(envelope,malicious);
            using(var stream=new MemoryStream())
            {
                new DataContractJsonSerializer(type).WriteObject(stream,envelope);
                try{BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(stream.ToArray()));throw new Exception("Over-budget invalid legacy sources accepted");}
                catch(InvalidDataException error){Check(error.Message=="$buildworks_import.file_limit","Legacy parsing preceded portable source budget");}
            }
            var library=new CompositeBlueprintStore.LibraryData { blueprints=new System.Collections.Generic.List<CompositeBlueprintStore.Blueprint>{malicious} };
            string native=(string)typeof(CompositeBlueprintStore).GetMethod("Serialize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{library});
            string path=Path.Combine(directory,"over-budget-load.json");File.WriteAllText(path,native);var refused=new CompositeBlueprintStore(path);
            Check(!refused.TryAddCategory("blocked",out string reason) && reason.Contains("$buildworks_import.file_limit") && File.ReadAllText(path)==native,"Native legacy budget or preserved refusal changed");
            malicious.sourceDocuments.RemoveRange(1,3);
            library.blueprints=Enumerable.Repeat(malicious,22).ToList();
            string savePath=Path.Combine(directory,"over-budget-save.json");var store=new CompositeBlueprintStore(savePath);
            typeof(CompositeBlueprintStore).GetField("data",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(store,library);
            bool stopped=false;
            try{typeof(CompositeBlueprintStore).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(store,null);}
            catch(TargetInvocationException error){stopped=error.InnerException is InvalidDataException && error.InnerException.Message=="$buildworks_import.file_limit";}
            Check(stopped && !File.Exists(savePath),"Legacy library source quota did not precede save/invalid JSON parsing");
        }
        internal static void Actual(string input,string output)
        {
            if(Directory.Exists(output))throw new IOException("Refusing to replace legacy export evidence");
            var paths=Directory.GetFiles(input,"*.json").Where(p=>!p.EndsWith(".dto.json",StringComparison.Ordinal) &&
                !p.EndsWith(".bits.json",StringComparison.Ordinal) && Path.GetFileName(p)!="receipt.json").ToArray();
            Check(paths.Length==5,"Expected five original current-codec JSON files");Directory.CreateDirectory(output);
            foreach(string path in paths)
            {
                string name=Path.GetFileNameWithoutExtension(path),folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
                byte[] original=File.ReadAllBytes(path);var source=ExternalBlueprintLibrary.LoadFile(path).Single();
                Check(source.importError==null,"Actual legacy JSON import: "+source.importError);RoundTrip(source,Path.Combine(folder,"roundtrip"));
                var review=BetterBuildingExport.Review(source,Basis,BetterBuildingExport.Target.LegacyJson);
                Check(review.Blockers.Count==0,"Actual legacy export blocked");
                Check(review.TryWrite(source,Basis,folder,"pair",out string pair,out _),"Actual legacy export failed");
                var native=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(pair,"pair.buildworks.json")));
                Check(native.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original),"Actual legacy original lost in full copy");
                if(name=="qa-snapshot-v3")
                {
                    var edited=source.ImportCopy();edited.parts[0].position.x+=1;edited.parts[2].signText="Edited legacy JSON QA";edited.parts.Reverse();
                    var changed=BetterBuildingExport.Review(edited,Basis,BetterBuildingExport.Target.LegacyJson);
                    Check(changed.TryWrite(edited,Basis,folder,"edited",out _,out string error),error);
                }
                Console.WriteLine("PASS original legacy JSON backend/native/portable/paired raw-source exact "+name);
            }
        }
        private static void RoundTrip(CompositeBlueprintStore.Blueprint source,string directory)
        {
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"library.json");var store=new CompositeBlueprintStore(path);
            Check(store.TryGetOrCreatePlacementCopy(source,out var saved,out string error),error);
            var reopened=new CompositeBlueprintStore(path).All().Single();
            var portable=BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)));
            Check(reopened.parts.Count==source.parts.Count && portable.sourceDocuments.Single().text==source.sourceDocuments.Single().text,"Legacy native/portable raw roundtrip");
            foreach(var document in new[]{saved,saved.ImportCopy()})
            {
                if(!ReferenceEquals(document,saved))foreach(var p in document.parts){p.sourceDocumentId=null;p.sourceRecord=0;p.sourceFrameApplied=false;}
                string old=Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(document)).Replace("\"version\":"+BlueprintExchangeFormat.Version,"\"version\":21");
                try{BlueprintExchangeFormat.Decode(old);throw new Exception("Legacy source entered portable21");}catch(InvalidDataException){}
            }
            string nativeOld=File.ReadAllText(path).Replace("\"version\":"+CompositeBlueprintStore.FormatVersion,"\"version\":33");
            string oldPath=Path.Combine(directory,"old.json");File.WriteAllText(oldPath,nativeOld);var blocked=new CompositeBlueprintStore(oldPath);
            Check(blocked.All().Count==0 && !blocked.TryAddCategory("blocked",out _) && File.ReadAllText(oldPath)==nativeOld,"Legacy source entered native33 or mutated blocked library");
        }
    }
}

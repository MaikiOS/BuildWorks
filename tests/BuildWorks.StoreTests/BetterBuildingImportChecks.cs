using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using Bbp = OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class BetterBuildingImportChecks
    {
        private static CompositeBlueprintStore.VectorData? Basis(string _) => new CompositeBlueprintStore.VectorData{x=2,y=3,z=4};
        private static void Check(bool value,string reason) { if(!value)throw new Exception(reason); }
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var rotation=KgBlueprintFrame.Euler(new Point3(15,40,25));
            var rows=new[]{new Bbp.Piece(1,"itemstand",new Point3(2,3,4),rotation,null,new Bbp.Attachment("SwordIron",7,3)),
                new Bbp.Piece(2,"sign",new Point3(-1,2,5),new Rotation3(0,0,0,1),"",scale:new Point3(1.5,.5,2))};
            byte[] bytes=Bbp.WriteBinary(4,"BBP source",rows);
            string path=Path.Combine(directory,"input.bbp");File.WriteAllBytes(path,bytes);
            var source=ExternalBlueprintLibrary.LoadFile(path).Single();
            Check(source.importError==null && source.parts.Count==2,"BBP file dispatch: "+source.importError);
            Check(source.parts[0].sourceRecord==1 && source.parts[1].signText=="" && source.parts[1].scale.x==1.5f &&
                source.parts.All(p=>p.sourceFrameApplied && !p.scaleIsAbsolute) && CompositeBlueprintStore.ValidSources(source.parts,source.sourceDocuments),"BBP frame/scale/sign/binding");
            Check(source.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes),"BBP original bytes");
            var zipPath=Path.Combine(directory,"input.zip");
            using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create))using(var stream=zip.CreateEntry("nested/input.bbp").Open())stream.Write(bytes,0,bytes.Length);
            var zipped=ExternalBlueprintLibrary.LoadFile(zipPath).Single();
            Check(zipped.importError==null && zipped.sourceDocuments.Single().id==source.sourceDocuments.Single().id,"ZIP source identity differs");
            RoundTrip(source,Path.Combine(directory,"roundtrip"));
            var original=source.sourceDocuments.Single().ToGeometry().BetterBuilding;
            var review=BetterBuildingExport.Review(source,Basis);Check(review.Blockers.Count==0 && review.Attachments==1 && review.Scales==1,"BBP review fields");
            Check(review.TryWrite(source,Basis,directory,"unchanged",out string result,out string error),error);
            var written=Bbp.ParseBinary(File.ReadAllBytes(Path.Combine(result,"unchanged.bbp")));
            Check(written.Pieces[0].Rotation.X==original.Pieces[0].Rotation.X && written.Pieces[0].Rotation.W==original.Pieces[0].Rotation.W,"Unedited raw quaternion changed");
            var edited=source.ImportCopy();edited.parts.Reverse();edited.parts[0].position.x+=.25f;
            var duplicate=CompositeBlueprintStore.CloneParts(new[]{edited.parts[1]}).Single();duplicate.stableId="duplicate";duplicate.position.z+=2;edited.parts.Add(duplicate);
            var changed=BetterBuildingExport.Review(edited,Basis);Check(changed.Blockers.Count==0,"Edited BBP refused");
            Check(changed.TryWrite(edited,Basis,directory,"edited",out result,out error),error);
            var newModel=Bbp.ParseBinary(File.ReadAllBytes(Path.Combine(result,"edited.bbp")));
            Check(newModel.Pieces.Count==3 && newModel.Pieces[0].SignText=="" && newModel.Pieces[0].Scale.Value.X==1.5 &&
                newModel.Pieces[1].DisplayItem.Prefab=="SwordIron" && newModel.Pieces[2].DisplayItem.Orientation==3 && newModel.Pieces[0].Position.X==-.75,
                "Reordered/duplicated optional indices or transform lost");
            var native=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(result,"edited.buildworks.json")));
            Check(native.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes),"Paired native archive lost original");
            var stale=BetterBuildingExport.Review(source,Basis);var moved=source.ImportCopy();moved.parts[0].position.y+=1;
            Check(!stale.TryWrite(moved,Basis,directory,"stale",out _,out _) && !Directory.Exists(Path.Combine(directory,"stale")),"Stale review published");
            var cancelled=BetterBuildingExport.Review(source,Basis);cancelled.CancelWrite();bool stopped=false;
            try{cancelled.TryWrite(source,Basis,directory,"cancelled",out _,out _);}catch(OperationCanceledException){stopped=true;}
            Check(stopped && !Directory.Exists(Path.Combine(directory,"cancelled")),"Cancelled pair published");
            Check(!review.TryWrite(source,Basis,directory,"unchanged",out _,out _),"Existing pair overwritten");
            var absolute=source.ImportCopy();absolute.parts[1].scaleIsAbsolute=true;absolute.parts[1].scale=new CompositeBlueprintStore.VectorData{x=1,y=3,z=2};
            var absoluteReview=BetterBuildingExport.Review(absolute,Basis);Check(absoluteReview.Blockers.Count==0,"Known intrinsic scale conversion failed");
            Check(absoluteReview.TryWrite(absolute,Basis,directory,"absolute",out result,out error),error);
            var scale=Bbp.ParseBinary(File.ReadAllBytes(Path.Combine(result,"absolute.bbp"))).Pieces[1].Scale.Value;
            Check(scale.X==.5 && scale.Y==1 && scale.Z==.5,"Absolute/intrinsic ratio lost");
            Check(BetterBuildingExport.Review(absolute,_=>null).Blockers.Any(b=>b.Reason==BetterBuildingExport.BlockReason.PrefabScaleUnknown),"Unknown intrinsic invented");
            var grid=new Bbp.TerrainProfile(2,3,1,1,-1,-1,new float[9]);
            var terrain=ExternalBlueprintLibrary.Parse(Convert.ToBase64String(Bbp.WriteBinary(4,"terrain",rows,grid)),"terrain.bbp");
            Check(BetterBuildingExport.Review(terrain,Basis).Blockers.Count==0,"Unchanged terrain rejected");
            var terrainEdited=terrain.ImportCopy();terrainEdited.parts[0].position.x+=.5f;
            Check(BetterBuildingExport.Review(terrainEdited,Basis).Blockers.Any(b=>b.Reason==BetterBuildingExport.BlockReason.TerrainFrame),"Stale terrain frame exported");
            terrainEdited=terrain.ImportCopy();terrainEdited.parts.RemoveAt(0);
            Check(BetterBuildingExport.Review(terrainEdited,Basis).Blockers.Any(b=>b.Reason==BetterBuildingExport.BlockReason.TerrainFrame),"Deleted-source terrain exported");
            byte[] budgetBytes=Bbp.WriteBinary(3,"budget",new[]{new Bbp.Piece(0,"sign",new Point3(),new Rotation3(0,0,0,1),new string('x',1024))});
            int expanded=Bbp.ParseBinary(budgetBytes).RawByteCount;Check(expanded>budgetBytes.Length,"Budget fixture must be compressed");
            string budgetPath=Path.Combine(directory,"budget.bbp");File.WriteAllBytes(budgetPath,budgetBytes);long budget=expanded*2-1;
            Check(ExternalBlueprintLibrary.LoadFile(budgetPath,ref budget).Single().importError==null,"First expanded source rejected");
            Check(ExternalBlueprintLibrary.LoadFile(budgetPath,ref budget).Single().importError=="$buildworks_import.file_limit","Batch budget charged compressed BBP only");
            var cacheSource=ExternalBlueprintLibrary.LoadFile(budgetPath).Single();
            var cache=new System.Collections.Generic.Dictionary<string,CompositeBlueprintStore.Blueprint>{{"budget.bbp",cacheSource}};
            var readMethod=typeof(ExternalBlueprintLibrary).GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static);
            using(var stream=new MemoryStream(budgetBytes))
            {
                object[] call={stream,(long)budgetBytes.Length,"budget.bbp",(long)expanded,cache};
                var warm=(CompositeBlueprintStore.Blueprint)readMethod.Invoke(null,call);
                Check(warm.importError==null && (long)call[3]==0 && ReferenceEquals(warm.sourceDocuments.Single().ToGeometry(),cacheSource.sourceDocuments.Single().ToGeometry()),"Warm cache did not charge exact expanded budget");
            }
            using(var stream=new MemoryStream(budgetBytes))
            {
                object[] call={stream,(long)budgetBytes.Length,"budget.bbp",(long)expanded-1,cache};
                Check(((CompositeBlueprintStore.Blueprint)readMethod.Invoke(null,call)).importError=="$buildworks_import.file_limit","Warm cache bypassed expanded budget");
            }
            byte[] big=Bbp.WriteBinary(3,"archived source",new[]{new Bbp.Piece(0,"sign",new Point3(),new Rotation3(0,0,0,1),new string('x',4096))});
            int bigRaw=Bbp.ParseBinary(big).RawByteCount;var archived=ExternalBlueprintLibrary.Parse(Convert.ToBase64String(big),"archived.bbp");
            foreach(var p in archived.parts){p.signText=null;p.sourceDocumentId=null;p.sourceRecord=0;p.sourceFrameApplied=false;}
            byte[] portable=BlueprintExportPair.Snapshot(archived).Native;Check(portable.Length<bigRaw,"Portable budget fixture must retain compact compressed source");
            string portablePath=Path.Combine(directory,"budget.buildworks.json");File.WriteAllBytes(portablePath,portable);budget=bigRaw*2-1;
            Check(ExternalBlueprintLibrary.LoadFile(portablePath,ref budget).Single().importError==null,"First expanded portable source rejected");
            Check(ExternalBlueprintLibrary.LoadFile(portablePath,ref budget).Single().importError=="$buildworks_import.file_limit","Portable BBP archive bypassed expanded batch budget");
            string sourceId=archived.sourceDocuments.Single().id;
            string wrongId=sourceId.Substring(0,sourceId.Length-1)+(sourceId.EndsWith("0")?"1":"0");
            string malformed=Encoding.UTF8.GetString(portable).Replace(sourceId,wrongId);
            string invalidPath=Path.Combine(directory,"invalid-budget.buildworks.json");File.WriteAllText(invalidPath,malformed,new UTF8Encoding(false));budget=bigRaw*2-1;
            Check(ExternalBlueprintLibrary.LoadFile(invalidPath,ref budget).Single().importError=="$buildworks_import.document_invalid","Malformed source identity accepted");
            Check(ExternalBlueprintLibrary.LoadFile(invalidPath,ref budget).Single().importError=="$buildworks_import.file_limit","Invalid portable BBP skipped expanded-budget charge");
            byte[] kg;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write("KG");writer.Write("");writer.Write("");for(int i=0;i<3;++i)writer.Write(0f);writer.Write(1);writer.Write(true);writer.Write("wood_floor");
                for(int i=0;i<6;++i)writer.Write(0f);writer.Write(false);writer.Write(0);writer.Write("");writer.Flush();kg=stream.ToArray();
            }
            var mixed=archived.ImportCopy();mixed.sourceDocuments.Add(CompositeBlueprintStore.SourceDocument.FromGeometry(new BlueprintSourceDocument(KgBlueprint.BinaryFormat,"mixed.oprint",Convert.ToBase64String(kg))));
            byte[] mixedBytes=BlueprintExportPair.Snapshot(mixed).Native;budget=bigRaw+kg.Length;Check(mixedBytes.Length<budget,"Mixed binary budget fixture must be compact");
            string mixedPath=Path.Combine(directory,"mixed-budget.buildworks.json");File.WriteAllBytes(mixedPath,mixedBytes);
            Check(ExternalBlueprintLibrary.LoadFile(mixedPath,ref budget).Single().importError==null && budget==0,"Mixed KG binary source charged its Base64 transport instead of bytes");
            // Header-only oversized-work fixture is deliberately invalid; rejection must precede model inflation/identity checks.
            byte[] declared=(byte[])big.Clone();Array.Copy(BitConverter.GetBytes(Bbp.MaximumRawBytes),0,declared,8,4);
            var oversized=BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable));
            oversized.sourceDocuments.Single().text=Convert.ToBase64String(declared);
            var nativeData=new CompositeBlueprintStore.LibraryData { blueprints=Enumerable.Repeat(oversized,17).ToList() };
            string nativeJson=(string)typeof(CompositeBlueprintStore).GetMethod("Serialize",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{nativeData});
            string nativePath=Path.Combine(directory,"over-budget-native.json");File.WriteAllText(nativePath,nativeJson,new UTF8Encoding(false));
            var denied=new CompositeBlueprintStore(nativePath);
            Check(!denied.TryAddCategory("blocked",out string deniedError) && deniedError.Contains("$buildworks_import.file_limit") && File.ReadAllText(nativePath)==nativeJson,"Native library did not preflight total expanded source quota");
            string savePath=Path.Combine(directory,"over-budget-save.json");var saveStore=new CompositeBlueprintStore(savePath);
            typeof(CompositeBlueprintStore).GetField("data",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(saveStore,nativeData);
            bool refused=false;
            try{typeof(CompositeBlueprintStore).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(saveStore,null);}
            catch(TargetInvocationException failure){refused=failure.InnerException is InvalidDataException && failure.InnerException.Message=="$buildworks_import.file_limit";}
            Check(refused && !File.Exists(savePath),"Native over-budget save published a file");
            Console.WriteLine("PASS BBP file/ZIP/source binding/native33portable21/edited pair/optional indices/scale/terrain guards/cancel/stale; no game proof");
        }
        internal static void Actual(string input,string output)
        {
            if(Directory.Exists(output))throw new IOException("Evidence output must be fresh");
            Directory.CreateDirectory(output);var paths=Directory.GetFiles(input,"*.bbp");Check(paths.Length==5,"Expected five original-writer BBP files");
            foreach(string path in paths)
            {
                string name=Path.GetFileNameWithoutExtension(path),folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
                byte[] original=File.ReadAllBytes(path);var source=ExternalBlueprintLibrary.LoadFile(path).Single();
                Check(source.importError==null,"Actual BBP import: "+source.importError);
                RoundTrip(source,Path.Combine(folder,"roundtrip"));
                var review=BetterBuildingExport.Review(source,Basis);Check(review.Blockers.Count==0,"Actual BBP export blockers "+name);
                Check(review.TryWrite(source,Basis,folder,"pair",out string pair,out string error),error);
                byte[] exported=File.ReadAllBytes(Path.Combine(pair,"pair.bbp")); Bbp.ParseBinary(exported);
                Check(Payload(exported).SequenceEqual(Payload(original)),"Unedited BBP raw payload mismatch "+name);
                var native=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(pair,"pair.buildworks.json")));
                Check(native.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original),"Actual paired archive lost original");
                if(name=="qa-snapshot-v3")
                {
                    var edited=source.ImportCopy();edited.parts[0].position.x+=1;edited.parts[2].signText="Edited BBP QA";
                    edited.parts.Reverse();var changed=BetterBuildingExport.Review(edited,Basis);Check(changed.Blockers.Count==0,"Actual edited BBP refused");
                    Check(changed.TryWrite(edited,Basis,folder,"edited",out _,out error),error);
                }
                Console.WriteLine("PASS original BBP backend native/portable/pair raw exact "+name+" compressedByteEqual="+exported.SequenceEqual(original));
            }
        }
        private static void RoundTrip(CompositeBlueprintStore.Blueprint source,string directory)
        {
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"library.json");var store=new CompositeBlueprintStore(path);
            Check(store.TryGetOrCreatePlacementCopy(source,out var saved,out string error),error);
            var reopened=new CompositeBlueprintStore(path).All().Single();
            var portable=BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)));
            Check(reopened.parts.Count==source.parts.Count && portable.sourceDocuments.Single().text==source.sourceDocuments.Single().text,"Native/portable source roundtrip");
            void Downgrade(CompositeBlueprintStore.Blueprint blueprint)
            {
                string old=Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(blueprint)).Replace("\"version\":"+BlueprintExchangeFormat.Version,"\"version\":20");
                try{BlueprintExchangeFormat.Decode(old);throw new Exception("BBP entered portable20");}catch(InvalidDataException){}
            }
            Downgrade(saved);var unbound=saved.ImportCopy();foreach(var p in unbound.parts){p.sourceDocumentId=null;p.sourceRecord=0;p.sourceFrameApplied=false;}
            Downgrade(unbound);
            string nativeOld=File.ReadAllText(path).Replace("\"version\":"+CompositeBlueprintStore.FormatVersion,"\"version\":32");
            string oldPath=Path.Combine(directory,"old.json");File.WriteAllText(oldPath,nativeOld);var blocked=new CompositeBlueprintStore(oldPath);
            Check(blocked.All().Count==0 && !blocked.TryAddCategory("blocked",out _) && File.ReadAllText(oldPath)==nativeOld,"BBP entered native32 or mutated blocked file");
            var wrong=CompositeBlueprintStore.CloneParts(saved.parts);wrong[0].sourceRecord=source.parts.Count+1;
            Check(!CompositeBlueprintStore.ValidSources(wrong,saved.sourceDocuments),"Invalid source record accepted");
            var evidence=saved.sourceDocuments.Single().Copy();evidence.id="wrong";
            try{evidence.ToGeometry();throw new Exception("Tampered cached source identity accepted");}catch(ArgumentException){}
        }
        private static byte[] Payload(byte[] file)
        {
            using(var input=new MemoryStream(file,48,file.Length-48,false))using(var deflate=new DeflateStream(input,CompressionMode.Decompress))using(var raw=new MemoryStream())
            { deflate.CopyTo(raw);return raw.ToArray(); }
        }
    }
}

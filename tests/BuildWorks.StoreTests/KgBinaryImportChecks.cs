using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class KgBinaryImportChecks
    {
        private static CompositeBlueprintStore.VectorData? Unit(string _) => new CompositeBlueprintStore.VectorData{x=1,y=1,z=1};
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"own.oprint");
            File.WriteAllBytes(path,Fixture());
            var source=ExternalBlueprintLibrary.LoadFile(path).Single();
            Require(source.importError==null && source.parts.Count==2,"Binary file dispatch: "+source.importError);
            Require(source.parts[0].prefabName=="kg_hash_706748652" && source.parts[1].prefabName=="706748652",
                "Binary numeric string/hash branches merged");
            var prepared=ExternalBlueprintLibrary.PrepareForHost(source,_=>null,ForeignZdoChecks.Hash,
                resolvePrefabHash:id=>id==706748652?"Piece_grausten_floor_1x1":null);
            Require(prepared.importError==null && prepared.parts[0].prefabName=="Piece_grausten_floor_1x1" &&
                prepared.parts[1].prefabName=="706748652" && CompositeBlueprintStore.ValidSources(prepared.parts,prepared.sourceDocuments),
                "Binary host lookup/binding changed identity");
            RoundTrip(source,Path.Combine(directory,"roundtrip"));
            var zipPath=Path.Combine(directory,"binary.zip");
            using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create))
            using(var stream=zip.CreateEntry("folder/source.oprint").Open())
            {byte[] bytes=Fixture();stream.Write(bytes,0,bytes.Length);}
            var zipped=ExternalBlueprintLibrary.LoadFile(zipPath).Single();
            Require(zipped.importError==null && zipped.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(Fixture()),
                "ZIP binary branch decoded UTF8 or lost original bytes");
            var review=KgExport.Review(source,Unit);
            Require(review.Blockers.Count==0 && review.ForeignExtension==".oprint","Binary export not selected");
            Require(review.TryWrite(source,Unit,directory,"paired",out string result,out string error),error);
            var output=KgBlueprint.ParseBinary(File.ReadAllBytes(Path.Combine(result,"paired.oprint")));
            Require(output.Objects[0].PrefabHash==706748652 && output.Objects[1].PrefabName=="706748652" &&
                output.Objects[1].PrefabHash==null,"Editable export changed binary Id branch");
            Require(File.Exists(Path.Combine(result,"paired.buildworks.json")) && !File.Exists(Path.Combine(result,"paired.yml")),
                "Binary review wrote the wrong file pair");
            var cancelled=KgExport.Review(source,Unit);cancelled.CancelWrite();
            bool stopped=false;
            try{cancelled.TryWrite(source,Unit,directory,"cancelled",out _,out _);}catch(OperationCanceledException){stopped=true;}
            Require(stopped && !Directory.Exists(Path.Combine(directory,"cancelled")),
                "Cancelled binary pair wrote files");
            int overhead=Fixture("").Length;
            byte[] maximum=Fixture(new string('A',BlueprintSourceDocument.MaximumBytes-overhead-3));
            Require(maximum.Length==BlueprintSourceDocument.MaximumBytes,"Binary maximum fixture differs");
            string maximumPath=Path.Combine(directory,"maximum.oprint");File.WriteAllBytes(maximumPath,maximum);
            var maximumSource=ExternalBlueprintLibrary.LoadFile(maximumPath).Single();
            Require(maximumSource.importError==null,"8MiB raw binary file rejected");
            var maximumStore=new CompositeBlueprintStore(Path.Combine(directory,"maximum-library.json"));
            Require(maximumStore.TryGetOrCreatePlacementCopy(maximumSource,out var maximumSaved,out error),error);
            var maximumReopened=BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(maximumSaved)));
            Require(maximumReopened.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(maximum),
                "8MiB binary raw / 11,184,812-char transport failed native/portable quota");
            Console.WriteLine("PASS binary file/ZIP/host Id/source/native32portable20/downgrade/paired export/cancel; no game execution");
        }
        internal static void Actual(string path,string directory)
        {
            if(Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Binary evidence output must be fresh.");
            Directory.CreateDirectory(directory);
            byte[] bytes=File.ReadAllBytes(path);
            var source=ExternalBlueprintLibrary.LoadFile(path).Single();
            Require(source.importError==null && source.parts.Count==4,"Actual binary import: "+source.importError);
            var original=source.sourceDocuments.Single().ToGeometry();
            Require(original.Format==KgBlueprint.BinaryFormat && original.OriginalBytes().SequenceEqual(bytes) &&
                original.ByteCount==bytes.Length && CompositeBlueprintStore.ValidSources(source.parts,source.sourceDocuments),
                "Actual binary original/record binding mismatch");
            RoundTrip(source,Path.Combine(directory,"roundtrip"));
            var edited=source.ImportCopy();edited.parts[0].position.x+=1;
            var review=KgExport.Review(edited,Unit);
            Require(review.Blockers.Count==0 && review.ForeignExtension==".oprint","Actual binary export rejected");
            Require(review.TryWrite(edited,Unit,directory,"edited",out string result,out string error),error);
            var exported=ExternalBlueprintLibrary.LoadFile(Path.Combine(result,"edited.oprint")).Single();
            Require(exported.importError==null && exported.parts.Count==edited.parts.Count,"Edited binary reimport rejected");
            Require(Math.Abs(exported.parts[0].position.x-edited.parts[0].position.x)<.0001,"Edited binary transform lost");
            var native=BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(result,"edited.buildworks.json")));
            Require(native.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes),"Native pair lost binary original");
            Require(File.ReadAllBytes(path).SequenceEqual(bytes),"Producer original mutated");
            Console.WriteLine("Binary editable pair: "+result);
            Console.WriteLine("PASS actual binary4: exact original/native/portable + edited .oprint pair; original-consumer read of edited output pending");
        }
        private static void RoundTrip(CompositeBlueprintStore.Blueprint source,string directory)
        {
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"library.json");
            var store=new CompositeBlueprintStore(path);
            Require(store.TryGetOrCreatePlacementCopy(source,out var saved,out string error),error);
            byte[] portable=BlueprintExchangeFormat.Encode(saved);
            var decoded=BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable));
            var reopened=new CompositeBlueprintStore(path).All().Single();
            Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(decoded)) && portable.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)),
                "Binary native/portable mismatch");
            Require(reopened.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(source.sourceDocuments.Single().ToGeometry().OriginalBytes()),
                "Native binary bytes lost");
            RejectDowngrade(saved,19);
            var sourceOnly=saved.ImportCopy();
            foreach(var part in sourceOnly.parts){part.sourceDocumentId=null;part.sourceRecord=0;part.sourceFrameApplied=false;part.foreignData=null;}
            RejectDowngrade(sourceOnly,19);
            var foreignOnly=sourceOnly.ImportCopy();foreignOnly.sourceDocuments=null;
            foreignOnly.parts[0].foreignData=CompositeBlueprintStore.ForeignPartData.FromGeometry(
                new BlueprintForeignPartData(KgBlueprint.BinaryFormat,null,"AAAAAAAAAA==",null));
            RejectDowngrade(foreignOnly,19);
            string old=File.ReadAllText(path).Replace("\"version\":"+CompositeBlueprintStore.FormatVersion,"\"version\":31");
            string oldPath=Path.Combine(directory,"native31.json");File.WriteAllText(oldPath,old);
            var blocked=new CompositeBlueprintStore(oldPath);
            Require(blocked.All().Count==0 && !blocked.TryAddCategory("Blocked",out _) && File.ReadAllText(oldPath)==old,"Binary profile entered native31");
            var invalid=CompositeBlueprintStore.CloneParts(saved.parts);invalid[0].sourceRecord=source.parts.Count+1;
            Require(!CompositeBlueprintStore.ValidSources(invalid,saved.sourceDocuments),"Binary invalid record accepted");
            invalid=CompositeBlueprintStore.CloneParts(saved.parts);invalid[0].prefabName="wrong";
            Require(!CompositeBlueprintStore.ValidSources(invalid,saved.sourceDocuments),"Binary wrong prefab binding accepted");
            var cached=saved.sourceDocuments.Single();var copy=cached.Copy();copy.text=Convert.ToBase64String(Fixture());copy.id="wrong";
            try{copy.ToGeometry();throw new Exception("Changed source identity accepted cached model");}catch(ArgumentException){}
        }
        private static void RejectDowngrade(CompositeBlueprintStore.Blueprint source,int version)
        {
            string text=Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(source));
            text=text.Replace("\"version\":"+BlueprintExchangeFormat.Version,"\"version\":"+version);
            try{BlueprintExchangeFormat.Decode(text);throw new Exception("Binary profile bypassed portable version gate");}catch(InvalidDataException){}
        }
        private static byte[] Fixture(string description="description")
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
            {
                writer.Write("Own binary");writer.Write("author");writer.Write(description);
                writer.Write(10f);writer.Write(45f);writer.Write(20f);writer.Write(2);
                for(int i=0;i<2;i++)
                {
                    writer.Write(i==1);if(i==0)writer.Write(706748652);else writer.Write("706748652");
                    for(int j=0;j<6;j++)writer.Write((float)j);
                    writer.Write(false);
                }
                writer.Write(0);writer.Write("");return stream.ToArray();
            }
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}

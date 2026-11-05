using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed class KgExport
    {
        internal enum BlockReason { MultipleSources, Scale, PrefabScaleUnknown, SourceBinding, StateAdapter, StateType, Precision }
        internal readonly List<(BlockReason Reason,string Detail)> Blockers=new List<(BlockReason,string)>();
        internal int Parts,Sources,OtherSources,Signs,MaterialSeeds,RawStates,ArchivedFields,Groups,Supports,Anchors,SourceImages;
        internal string NumericCulture;
        internal string ForeignExtension = ".yml";
        private string sourceFormat;
        private byte[] native,foreign;
        private BlueprintExportPair files;
        internal void CancelWrite()=>files?.CancelWrite();

        internal static KgExport Review(CompositeBlueprintStore.Blueprint source,
            Func<string,CompositeBlueprintStore.VectorData?> resolveScale)
        {
            var snapshot=BlueprintExportPair.Snapshot(source);
            var document=snapshot.Document;
            var roots=(document.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>())
                .Where(item=>KgBlueprint.IsFormat(item.format)).ToArray();
            var review=new KgExport{native=snapshot.Native,Parts=document.parts.Count,Sources=document.sourceDocuments?.Count ?? 0,
                OtherSources=(document.sourceDocuments?.Count ?? 0)-roots.Length,Groups=document.groups.Count,
                Supports=document.supports?.Count ?? 0,Anchors=document.anchors.Count,
                Signs=document.parts.Count(part=>part.signText!=null),MaterialSeeds=document.parts.Count(part=>part.materialSeed.HasValue),
                NumericCulture=CultureInfo.CurrentCulture.Name};
            if(roots.Length>1){review.Blockers.Add((BlockReason.MultipleSources,roots.Length.ToString(CultureInfo.InvariantCulture)));return review;}
            var evidence=roots.SingleOrDefault()?.ToGeometry();
            var model=evidence?.Kg ?? KgBlueprint.Parse("Name: BuildWorks export\nAuthor: BuildWorks\nDescription: Full source credits and construction data are in the adjacent native document.\nObjects: [{Id: template}]\n");
            review.sourceFormat=model.IsBinary ? KgBlueprint.BinaryFormat : KgBlueprint.Format;
            review.ForeignExtension=model.IsBinary ? ".oprint" : ".yml";
            review.SourceImages=(!string.IsNullOrEmpty(model.Icon)?1:0)+model.Previews.Count;
            var rows=new List<KgBlueprint.Piece>();
            var scales=new Dictionary<string,CompositeBlueprintStore.VectorData?>(StringComparer.Ordinal);
            foreach(var part in document.parts)
            {
                bool unit=!part.scaleIsAbsolute && !part.baseScale.HasValue && part.scale.x==1 && part.scale.y==1 && part.scale.z==1;
                if(!unit)
                {
                    if(!scales.TryGetValue(part.prefabName,out var basis))scales.Add(part.prefabName,basis=resolveScale?.Invoke(part.prefabName));
                    if(!basis.HasValue || !CompositeBlueprintStore.PositiveScale(basis.Value))review.Blockers.Add((BlockReason.PrefabScaleUnknown,part.prefabName));
                    else
                    {
                        var actual=part.RootScale(basis.Value.ToVector3());
                        if(actual.x!=basis.Value.x || actual.y!=basis.Value.y || actual.z!=basis.Value.z)review.Blockers.Add((BlockReason.Scale,part.stableId));
                    }
                }
                KgBlueprint.Piece original=null;
                if(evidence!=null && part.sourceDocumentId==evidence.Id)
                {
                    if(!part.sourceFrameApplied || !evidence.HasPartAddress(part.sourceLine,part.sourceRecord,part.originalPrefabName ?? part.prefabName))
                    {review.Blockers.Add((BlockReason.SourceBinding,part.stableId));continue;}
                    original=model.Objects[part.sourceRecord-1];
                }
                string state=State(part,original,review);
                var position=new Point3(part.position.x,part.position.y,part.position.z);
                var rotation=new Rotation3(part.rotation.x,part.rotation.y,part.rotation.z,part.rotation.w);
                var pose=KgBlueprintFrame.Stored(position,rotation,model.BoxRotation);
                if(original!=null && Unchanged(part,KgBlueprintFrame.Canonical(original,model.BoxRotation)))
                    pose=(original.Position,original.Euler);
                string id=(part.originalPrefabName==null?original?.Id:null) ?? (int.TryParse(part.prefabName,NumberStyles.Integer,CultureInfo.InvariantCulture,out _)
                    ? part.prefabName.GetStableHashCode().ToString(CultureInfo.InvariantCulture):part.prefabName);
                rows.Add(new KgBlueprint.Piece(original?.Record ?? 0,id,pose.Position,pose.Euler,state,
                    model.IsBinary ? null : part.originalPrefabName==null?original?.Prefab:part.prefabName,
                    model.IsBinary && original!=null && part.originalPrefabName==null ? (bool?)!original.PrefabHash.HasValue : null));
            }
            if(review.Blockers.Count>0)return review;
            review.foreign=model.IsBinary ? model.WriteBinaryObjects(rows,document.name) :
                new UTF8Encoding(false,true).GetBytes(model.WriteObjects(rows,document.name));
            var reopened=model.IsBinary ? KgBlueprint.ParseBinary(review.foreign) :
                KgBlueprint.Parse(new UTF8Encoding(false,true).GetString(review.foreign));
            for(int i=0;i<reopened.Objects.Count;i++)
            {
                var pose=KgBlueprintFrame.Canonical(reopened.Objects[i],reopened.BoxRotation);
                var part=document.parts[i];
                if(!Near(pose.Position,new Point3(part.position.x,part.position.y,part.position.z),.0001) ||
                    !SameOrientation(pose.Rotation,new Rotation3(part.rotation.x,part.rotation.y,part.rotation.z,part.rotation.w),.00001))
                    review.Blockers.Add((BlockReason.Precision,part.stableId));
            }
            if(review.Blockers.Count>0)return review;
            review.files=new BlueprintExportPair(review.native,review.foreign,review.ForeignExtension);
            return review;
        }

        private static string State(CompositeBlueprintStore.Part part,KgBlueprint.Piece original,KgExport review)
        {
            if(part.foreignData!=null && (part.foreignData.format!=review.sourceFormat || original==null || part.foreignData.zdo!=original.ZdoData))
            {review.Blockers.Add((BlockReason.StateAdapter,part.stableId));return null;}
            string raw=original?.ZdoData;
            if(raw==null && part.signText==null && !part.materialSeed.HasValue)return null;
            if(!BlueprintForeignZdo.TryReadKg(raw ?? "AAAAAAAAAA==",out var state))throw new InvalidDataException("$buildworks_import.document_invalid");
            var updates=new List<KeyValuePair<int,object>>();
            var omit=new List<KeyValuePair<int,Type>>();
            void Field(string key,object value,Type type)
            {
                int hash=key.GetStableHashCode();
                if(value!=null)
                {
                    if(state.HasHash(hash) && !state.Keys.Contains(new KeyValuePair<int,Type>(hash,type)))
                        review.Blockers.Add((BlockReason.StateType,part.stableId+" / "+key));
                    updates.Add(new KeyValuePair<int,object>(hash,value));
                }
                else if(part.foreignData==null)omit.Add(new KeyValuePair<int,Type>(hash,type));
            }
            Field("text",part.signText,typeof(string));Field("RandMatSeed",part.materialSeed,typeof(int));
            // These identify a previous live project, not a reusable blueprint. Exact originals stay in the native pair.
            foreach(var key in new[]{new KeyValuePair<int,Type>("BuildWorks_ExactRotation_v1".GetStableHashCode(),typeof(Rotation3)),
                new KeyValuePair<int,Type>("BuildWorks_ExactRotationWitness_v1".GetStableHashCode(),typeof(Rotation3)),
                new KeyValuePair<int,Type>("ostrix_buildworks_project_operation_v1".GetStableHashCode(),typeof(string)),
                new KeyValuePair<int,Type>("ostrix_buildworks_project_receipt_v1".GetStableHashCode(),typeof(byte[]))})
                if(state.Keys.Contains(key)){omit.Add(key);review.ArchivedFields++;}
            if(part.foreignData!=null)review.RawStates++;
            if(updates.Count==0 && !omit.Any(key=>state.Keys.Contains(key)))return raw;
            string written=state.WriteKg(updates,omit);
            return BlueprintForeignZdo.TryReadKg(written,out var check) && check.Count==0?null:written;
        }

        private static bool Unchanged(CompositeBlueprintStore.Part part,(Point3 Position,Rotation3 Rotation) pose)=>
            part.position.x==(float)pose.Position.X && part.position.y==(float)pose.Position.Y && part.position.z==(float)pose.Position.Z &&
            (part.rotation.x==(float)pose.Rotation.X && part.rotation.y==(float)pose.Rotation.Y && part.rotation.z==(float)pose.Rotation.Z && part.rotation.w==(float)pose.Rotation.W ||
             part.rotation.x==-(float)pose.Rotation.X && part.rotation.y==-(float)pose.Rotation.Y && part.rotation.z==-(float)pose.Rotation.Z && part.rotation.w==-(float)pose.Rotation.W);
        private static bool Near(Point3 a,Point3 b,double tolerance)=>Math.Abs(a.X-b.X)<=tolerance && Math.Abs(a.Y-b.Y)<=tolerance && Math.Abs(a.Z-b.Z)<=tolerance;
        private static bool SameOrientation(Rotation3 a,Rotation3 b,double tolerance)=>
            new[]{new Point3(1,0,0),new Point3(0,1,0),new Point3(0,0,1)}.All(axis=>Near(a.Rotate(axis),b.Rotate(axis),tolerance));

        internal bool TryWrite(CompositeBlueprintStore.Blueprint source,Func<string,CompositeBlueprintStore.VectorData?> resolveScale,
            string directory,string name,out string result,out string error)
        {
            result=null;error=null;
            if(files==null || Blockers.Count>0){error="$buildworks_import.transform";return false;}
            return files.TryWrite(()=>{
                if(CultureInfo.CurrentCulture.Name!=NumericCulture)throw new InvalidDataException("$buildworks_exchange.plan_changed");
                var current=Review(source,resolveScale);
                if(current.Blockers.Count>0)throw new InvalidDataException("$buildworks_exchange.plan_changed");
                return(current.native,current.foreign);
            },directory,name,out result,out error);
        }
    }
}

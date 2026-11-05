using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintSourcePresentation
    {
        private static readonly HashSet<KeyValuePair<int,Type>> KnownImageState=new HashSet<KeyValuePair<int,Type>> {
                new KeyValuePair<int,Type>("creator".GetStableHashCode(),typeof(long)),
                new KeyValuePair<int,Type>("creatorIndex".GetStableHashCode(),typeof(int)),
                new KeyValuePair<int,Type>("cheated".GetStableHashCode(),typeof(int)),
                new KeyValuePair<int,Type>("RandMatSeed".GetStableHashCode(),typeof(int)),
                new KeyValuePair<int,Type>("BuildWorks_ExactRotation_v1".GetStableHashCode(),typeof(Rotation3)),
                new KeyValuePair<int,Type>("BuildWorks_ExactRotationWitness_v1".GetStableHashCode(),typeof(Rotation3)),
                new KeyValuePair<int,Type>("ostrix_buildworks_project_operation_v1".GetStableHashCode(),typeof(string)),
                new KeyValuePair<int,Type>("ostrix_buildworks_project_receipt_v1".GetStableHashCode(),typeof(byte[]))};

        internal static string Metadata(CompositeBlueprintStore.Blueprint blueprint)
        {
            var sources=KgSources(blueprint);
            if(sources.Length==0)
            {
                try
                {
                    var unbound = (blueprint?.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>())
                        .Where(s => s.format == UnboundBlueprint.Format).Select(s => s.ToGeometry().Unbound).ToArray();
                    if (unbound.Length != 0)
                        return BuildWorksLocalization.Text("catalog.unbound_metadata", unbound.Sum(m => m.Pieces.Count),
                            unbound.Sum(m => m.Pieces.Sum(p => p.State.Count)), unbound.Sum(m => m.Terrain.Count));
                    var planners = (blueprint?.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>())
                        .Where(s => s.format == PlannerBlueprint.Format).Select(s => s.ToGeometry().Planner).ToArray();
                    if (planners.Length != 0)
                        return BuildWorksLocalization.Text("catalog.planner_metadata", planners.Sum(m => m.Pieces.Count),
                            planners.Sum(m => m.Layers.Count), planners.Sum(m => m.UnknownPaths.Count));
                    var family = (blueprint?.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>())
                        .Where(s => BetterBuildingBlueprint.IsFormat(s.format)).ToArray();
                    var bbp = family.Select(s => s.ToGeometry().BetterBuilding).ToArray();
                    if (bbp.Length == 0) return "";
                    return BuildWorksLocalization.Text(family.Any(s => s.format == BetterBuildingBlueprint.JsonFormat) ? "catalog.bbp_json_metadata" : "catalog.bbp_metadata",
                        bbp.Sum(m => m.Pieces.Count), bbp.Sum(m => m.Pieces.Count(p => p.SignText != null)),
                        bbp.Sum(m => m.Pieces.Count(p => p.DisplayItem != null)), bbp.Sum(m => m.Pieces.Count(p => p.Scale.HasValue)), bbp.Sum(m => m.Terrain?.Heights.Count ?? 0),
                        string.Join(" / ", family.Select(s => s.format == BetterBuildingBlueprint.JsonFormat ? "JSON" : "BBPF").Distinct()), bbp.Sum(m => m.UnknownPaths.Count));
                }
                catch(Exception error) when(error is ArgumentException || error is InvalidDataException)
                { return BuildWorksLocalization.Text("catalog.source_unavailable"); }
            }
            if(!BlueprintSignPreview.Allowed)return BuildWorksLocalization.Text("catalog.source_hidden");
            if(sources.Length!=1)return BuildWorksLocalization.Text("catalog.kg_multiple",sources.Length);
            try
            {
                var model=sources[0].ToGeometry().Kg;
                if(model==null)return BuildWorksLocalization.Text("catalog.source_unavailable");
                return BuildWorksLocalization.Text("catalog.kg_metadata",Literal(model.Author,128,1),
                    Literal(model.Description,1024,6))+"\n"+BuildWorksLocalization.Text("catalog.kg_original_image");
            }
            catch(Exception error) when(error is ArgumentException || error is InvalidDataException)
            {return BuildWorksLocalization.Text("catalog.source_unavailable");}
        }

        internal static string Literal(string text,int limit,int lines)
        {
            var bounded=new StringBuilder();int line=1;
            for(int i=0;i<Math.Min(text?.Length ?? 0,limit) && bounded.Length<limit;i++)
            {
                char c=text[i];
                if(c=='\r')continue;
                if(c=='\n' || c=='\u2028' || c=='\u2029')
                {if(line++>=lines)break;bounded.Append('\n');continue;}
                if(char.IsControl(c))continue;
                if(char.IsHighSurrogate(c))
                {
                    if(i+1<text.Length && char.IsLowSurrogate(text[i+1]) && bounded.Length+2<=limit)
                    {bounded.Append(c).Append(text[++i]);}
                    continue;
                }
                if(char.IsLowSurrogate(c))continue;
                bounded.Append(c);
            }
            CensorShittyWords.Filter(bounded.ToString(),out string filtered);
            return (filtered ?? "").Replace('<','＜').Replace('>','＞').Replace('\\','＼');
        }

        internal static CompositeBlueprintStore.SourceDocument[] KgSources(CompositeBlueprintStore.Blueprint blueprint)=>
            (blueprint?.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>())
                .Where(source=>KgBlueprint.IsFormat(source.format)).ToArray();

        internal static IEnumerable<byte[]> Images(CompositeBlueprintStore.Blueprint blueprint)
        {
            if(!BlueprintSignPreview.Allowed || blueprint.parts==null ||
                blueprint.parts.Any(part=>part.signText!=null))yield break;
            // shortcut: original Unbound packets need semantic image admission before sidecar previews can be displayed.
            if (blueprint.sourceDocuments?.Any(source => source?.format == UnboundBlueprint.Format) == true) yield break;
            foreach (var source in (blueprint.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>()).Where(s => s.format == PlannerBlueprint.Format))
            {
                PlannerBlueprint planner;
                try { planner = source.ToGeometry().Planner; }
                catch (Exception error) when (error is ArgumentException || error is InvalidDataException) { yield break; }
                if (planner == null || planner.UnknownPaths.Count != 0 || planner.Pieces.Any(p => p.Prefab == "sign")) yield break;
            }
            foreach (var source in (blueprint.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>()).Where(s => BetterBuildingBlueprint.IsFormat(s.format)))
            {
                BetterBuildingBlueprint bbp;
                try { bbp = source.ToGeometry().BetterBuilding; }
                catch(Exception error) when(error is ArgumentException || error is InvalidDataException) { yield break; }
                if (bbp == null || bbp.UnknownPaths.Count != 0 || bbp.Pieces.Any(p => p.Prefab == "sign" || p.SignText != null || p.DisplayItem != null)) yield break;
            }
            var sources=KgSources(blueprint);
            if(sources.Length>1)yield break;
            KgBlueprint model=null;
            if(sources.Length==1)
            {
                try{model=sources[0].ToGeometry().Kg;}
                catch(Exception error) when(error is ArgumentException || error is InvalidDataException){yield break;}
                // Source signs remain unsafe even before host projection or after clearing native text.
                if(model==null || model.Objects.Any(part=>part.MatchesPrefab("sign") || !ImageStateKnown(part.ZdoData)) ||
                    blueprint.parts.Any(part=>part.foreignData!=null))yield break;
            }
            if(model!=null)
            {
                if(model.Icon!=null)yield return Convert.FromBase64String(model.Icon);
                foreach(string preview in model.Previews)yield return Convert.FromBase64String(preview);
            }
            if(blueprint.importPreview!=null)yield return blueprint.importPreview;
        }

        private static bool ImageStateKnown(string raw)
        {
            if(raw==null)return true;
            if(!BlueprintForeignZdo.TryReadKg(raw,out var state))return false;

            return state.Keys.All(KnownImageState.Contains);
        }
    }
}

#if BUILDWORKS_TEST_STAND
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using G = OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Read-only host proof: no editor/catalog writes, object creation, movement or construction.
    internal static class BuildWorksTestKgBlueprint
    {
        public static string Check(string path)
        {
            var player=BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            var inventory=new ZPackage();player.GetInventory().Save(inventory);
            string before=Convert.ToBase64String(inventory.GetArray());
            var raw=ExternalBlueprintLibrary.LoadFile(path).Single();
            if(raw.importError!=null) throw new InvalidOperationException(raw.importError);
            var model=raw.sourceDocuments.Single().ToGeometry().Kg;
            var prepared=ExternalBlueprintLibrary.PrepareForHost(raw,name=>{
                var prefab=ZNetScene.instance.GetPrefab(name);
                return prefab?(CompositeBlueprintStore.VectorData?)new CompositeBlueprintStore.VectorData(prefab.transform.localScale):null;
            },name=>name.GetStableHashCode(),Game.m_worldLevel,
                name=>BlueprintMaterialState.Supports(ZNetScene.instance.GetPrefab(name)),
                name=>BlueprintStorageCapture.PrefabGrid(ZNetScene.instance.GetPrefab(name)),
                id=>{var prefab=ZNetScene.instance.GetPrefab(id);return prefab?prefab.name:null;},
                name=>BlueprintStorageCapture.SupportsKgStateProjection(ZNetScene.instance.GetPrefab(name)));
            if(prepared.importError!=null) throw new InvalidOperationException(prepared.importError);
            float maximumPositionError=0,maximumAxisError=0;
            var componentTypes=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var box=Quaternion.Euler(V(model.BoxRotation));
            for(int i=0;i<model.Objects.Count;i++)
            {
                var original=model.Objects[i];var actual=prepared.parts[i];
                Quaternion expected=Quaternion.Inverse(box)*Quaternion.Euler(V(original.Euler));
                var rotation=new Quaternion(actual.rotation.x,actual.rotation.y,actual.rotation.z,actual.rotation.w);
                maximumPositionError=Mathf.Max(maximumPositionError,Vector3.Distance(actual.position.ToVector3(),Quaternion.Inverse(box)*V(original.Position)));
                foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
                    maximumAxisError=Mathf.Max(maximumAxisError,Vector3.Distance(rotation*axis,expected*axis));
                var prefab=original.PrefabHash.HasValue?ZNetScene.instance.GetPrefab(original.PrefabHash.Value):ZNetScene.instance.GetPrefab(original.Id);
                if(!prefab || actual.prefabName!=prefab.name) throw new InvalidOperationException("KG native Id resolution differs.");
                foreach(var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    componentTypes.Add(component?component.GetType().AssemblyQualifiedName:"missing-script");
            }
            if(BlueprintStorageCapture.KgOrdinaryComponent(typeof(BuildWorksPlugin)))
                throw new InvalidOperationException("KG admitted an unknown custom behaviour.");
            if(maximumPositionError>.001f || maximumAxisError>.0001f) throw new InvalidOperationException("KG native frame differs: "+maximumPositionError+" / "+maximumAxisError);
            foreach(string name in new[]{"piece_chest_wood","itemstand","ArmorStand"})
            {
                var prefab=ZNetScene.instance.GetPrefab(name);
                if(!prefab || BlueprintStorageCapture.SupportsKgStateProjection(prefab)!=false)
                    throw new InvalidOperationException("KG special-state classifier failed: "+name);
            }
            var export=KgExport.Review(prepared,name=>{
                var prefab=ZNetScene.instance.GetPrefab(name);
                return prefab?(CompositeBlueprintStore.VectorData?)new CompositeBlueprintStore.VectorData(prefab.transform.localScale):null;
            });
            if(export.Blockers.Count!=0 || export.Parts!=prepared.parts.Count)
                throw new InvalidOperationException("Native KG export review rejected the verified source.");
            float maximumExportAxisError=0;
            foreach(float pitch in new[]{-90f,-89.999f,-25f,0f,25f,89.999f,90f})
            foreach(float yaw in new[]{-130f,0f,130f})
            {
                Quaternion rotation=Quaternion.Euler(pitch,yaw,-15f);
                var stored=G.KgBlueprintFrame.Stored(new G.Point3(0,0,0),
                    new G.Rotation3(rotation.x,rotation.y,rotation.z,rotation.w),model.BoxRotation);
                Quaternion restored=Quaternion.Inverse(box)*Quaternion.Euler(V(stored.Euler));
                foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
                    maximumExportAxisError=Mathf.Max(maximumExportAxisError,Vector3.Distance(rotation*axis,restored*axis));
            }
            if(maximumExportAxisError>.0001f)throw new InvalidOperationException("KG export differs from native Euler: "+maximumExportAxisError);
            string literal=BlueprintSourcePresentation.Literal("<b>BuildWorks</b>\\n",128,1);
            if(literal.Contains("<") || literal.Contains(">") || literal.Contains("\\"))
                throw new InvalidOperationException("Native metadata literal safety failed.");
            string metadata=BlueprintSourcePresentation.Metadata(prepared);
            if(string.IsNullOrEmpty(metadata))throw new InvalidOperationException("KG metadata is absent.");
            int images=BlueprintSourcePresentation.Images(prepared).Count();
            if(model.Objects.Any(part=>part.MatchesPrefab("sign")) && images!=0)
                throw new InvalidOperationException("Source sign allowed an embedded image.");
            var after=new ZPackage();player.GetInventory().Save(after);
            if(Convert.ToBase64String(after.GetArray())!=before || prepared.sourceDocuments.Single().text!=raw.sourceDocuments.Single().text)
                throw new InvalidOperationException("KG host check changed inventory/source.");
            var yaml=AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly=>assembly.GetName().Name=="YamlDotNet");
            if(!model.IsBinary && yaml==null)throw new InvalidOperationException("KG YAML parser binding is absent.");
            return JsonUtility.ToJson(new Result{parts=prepared.parts.Count,maximumPositionError=maximumPositionError,
                maximumAxisError=maximumAxisError,knownState=prepared.parts.All(part=>part.foreignData==null),
                specialClassifiers=3,inventoryUnchanged=true,sourceUnchanged=true,componentTypes=componentTypes.OrderBy(value=>value,StringComparer.Ordinal).ToArray(),
                yamlAssembly=yaml?.FullName,yamlPath=yaml?.Location,sourceFormat=raw.sourceDocuments.Single().format,
                sourceBytes=model.IsBinary?model.BinaryByteCount:new System.Text.UTF8Encoding(false,true).GetByteCount(model.Text),
                binarySource=model.IsBinary,exportExtension=export.ForeignExtension,exportReviewed=true,archivedFields=export.ArchivedFields,
                nativeExportCases=21,maximumExportAxisError=maximumExportAxisError,metadataAvailable=true,imageCandidates=images,
                userContentAllowed=BlueprintSignPreview.Allowed});
        }
        private static Vector3 V(G.Point3 p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        [Serializable] private sealed class Result
        {
            public int parts,specialClassifiers;
            public float maximumPositionError,maximumAxisError;
            public bool knownState,inventoryUnchanged,sourceUnchanged;
            public string[] componentTypes;
            public string yamlAssembly,yamlPath;
            public string sourceFormat,exportExtension;
            public bool binarySource;
            public int sourceBytes;
            public bool exportReviewed,metadataAvailable,userContentAllowed;
            public int archivedFields,nativeExportCases,imageCandidates;
            public float maximumExportAxisError;
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace OstrixMods.BuildWorks
{
    // A portable construction document, never Unity meshes, arbitrary ZDO, or executable assets.
    internal static class BlueprintExchangeFormat
    {
        internal const int MaximumBytes = 32 * 1024 * 1024;
        private const string FormatName = "buildworks-blueprint";
        private const int Version = 11;

        [DataContract]
        private sealed class Envelope
        {
            [DataMember(IsRequired = true)] public string format;
            [DataMember(IsRequired = true)] public int version;
            [DataMember(IsRequired = true)] public List<string> requiredPrefabIds;
            [DataMember(IsRequired = true)] public CompositeBlueprintStore.Blueprint blueprint;
        }

        private static readonly Dictionary<Type, Dictionary<string, Type>> Fields =
            new[] { typeof(Envelope), typeof(CompositeBlueprintStore.Blueprint), typeof(CompositeBlueprintStore.Part),
                typeof(CompositeBlueprintStore.Group), typeof(CompositeBlueprintStore.Support),
                typeof(CompositeBlueprintStore.SourceDocument), typeof(CompositeBlueprintStore.ForeignPartData), typeof(CompositeBlueprintStore.PrefabMetadata),
                typeof(CompositeBlueprintStore.VectorData), typeof(CompositeBlueprintStore.QuaternionData),
                typeof(CompositeBlueprintStore.LibraryData) }
                .ToDictionary(type => type, type => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(field => field.GetCustomAttribute<DataMemberAttribute>() != null)
                    .ToDictionary(field => field.GetCustomAttribute<DataMemberAttribute>().Name ?? field.Name,
                        field => field.FieldType, StringComparer.Ordinal));
        private static readonly Dictionary<Type, string[]> RequiredFields = Fields.Keys.ToDictionary(type => type,
            type => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => field.GetCustomAttribute<DataMemberAttribute>() is DataMemberAttribute member &&
                    (member.EmitDefaultValue || member.IsRequired))
                .Select(field => field.GetCustomAttribute<DataMemberAttribute>().Name ?? field.Name).ToArray());

        internal static void ValidateLibraryJson(string text, bool requireFields, bool allowSupportFields, bool allowSourceFields,
            bool allowForeignFields, bool allowContentsFields, bool allowAbsoluteScaleFields, bool allowPlacementImportFields,
            bool allowBaseScaleFields, bool allowMetadataFields = true, bool allowMaterialSeedFields = true, bool allowHiddenSources = true) => ValidateStructure(
            Encoding.UTF8.GetBytes(text), typeof(CompositeBlueprintStore.LibraryData), requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources);

        internal static string DecodeSignText(string token, bool allowLegacyLiteral = false)
        {
            string text = DecodeTextToken(token, allowLegacyLiteral);
            if (!Geometry.BlueprintEditorPart.IsValidSignText(text))
                throw new InvalidDataException("$buildworks_import.object_data");
            return text;
        }

        internal static string DecodeInventoryInfo(string token)
        {
            if (token == null || token.Length > 32768) throw new InvalidDataException("$buildworks_import.object_data");
            return DecodeTextToken(token, false);
        }

        private static string DecodeTextToken(string token, bool allowLegacyLiteral)
        {
            if (token == "" || token == "\"\"" || token == "null") return null;
            string text = token;
            if (token.TrimStart().StartsWith("\"", StringComparison.Ordinal))
            {
                try
                {
                    byte[] bytes = new UTF8Encoding(false, true).GetBytes(token);
                    ValidateStructure(bytes, typeof(string));
                    using (var input = new MemoryStream(bytes))
                        text = (string)new DataContractJsonSerializer(typeof(string)).ReadObject(input);
                }
                catch (Exception error) when (error is SerializationException || error is XmlException ||
                    error is ArgumentException || error is InvalidDataException)
                {
                    if (!allowLegacyLiteral) throw new InvalidDataException("$buildworks_import.object_data", error);
                    // PlanBuild keeps non-JSON additionalInfo verbatim, including a leading quote.
                    text = token;
                }
            }
            return text;
        }

        private static void ValidateStructure(byte[] bytes, Type expected, bool requireFields = true, bool allowSupportFields = true,
            bool allowSourceFields = true, bool allowForeignFields = true, bool allowContentsFields = true, bool allowAbsoluteScaleFields = true, bool allowPlacementImportFields = false, bool allowBaseScaleFields = true, bool allowMetadataFields = true, bool allowMaterialSeedFields = true, bool allowHiddenSources = true)
        {
            using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(bytes,
                new XmlDictionaryReaderQuotas
                {
                    MaxDepth = 16,
                    MaxStringContentLength = Geometry.BlueprintSourceDocument.MaximumBytes,
                    MaxArrayLength = bytes.Length,
                    MaxBytesPerRead = 8192,
                    MaxNameTableCharCount = 16384
                }))
            {
                reader.MoveToContent();
                ValidateNode(reader, expected, requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources);
                if (reader.MoveToContent() != XmlNodeType.None) throw new InvalidDataException("$buildworks_import.document_invalid");
            }
        }

        internal static byte[] Encode(CompositeBlueprintStore.Blueprint blueprint)
        {
            ValidateBlueprint(blueprint);
            if (blueprint.placementImportId != null)
            {
                blueprint = blueprint.ImportCopy();
                blueprint.placementImportId = null; // Local placement cache identity is not portable construction data.
            }
            using (var output = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Envelope)).WriteObject(output, new Envelope
                { format = FormatName, version = Version, requiredPrefabIds = Prefabs(blueprint), blueprint = blueprint });
                if (output.Length > MaximumBytes) throw new InvalidDataException("$buildworks_import.file_limit");
                return output.ToArray();
            }
        }

        internal static CompositeBlueprintStore.Blueprint Decode(string text)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > MaximumBytes)
                throw new InvalidDataException("$buildworks_import.file_limit");
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text.TrimStart('\uFEFF'));
            try
            {
                ValidateStructure(bytes, typeof(Envelope));
                Envelope envelope;
                using (var input = new MemoryStream(bytes))
                    envelope = (Envelope)new DataContractJsonSerializer(typeof(Envelope)).ReadObject(input);
                if (envelope == null || envelope.format != FormatName || envelope.version < 1 || envelope.version > Version)
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < Version)
                    ValidateStructure(bytes, typeof(Envelope), allowSupportFields: envelope.version >= 2,
                        allowSourceFields: envelope.version >= 3, allowForeignFields: envelope.version >= 4,
                        allowContentsFields: envelope.version >= 5, allowAbsoluteScaleFields: envelope.version >= 6,
                        allowBaseScaleFields: envelope.version >= 7, allowMetadataFields: envelope.version >= 9,
                        allowMaterialSeedFields: envelope.version >= 10);
                if (envelope.blueprint != null)
                    envelope.blueprint.supports = envelope.blueprint.supports ?? new List<CompositeBlueprintStore.Support>();
                if (envelope.version < 8 && envelope.blueprint?.parts?.Count == 1)
                    throw new InvalidDataException("$buildworks_import.part_limit");
                if (envelope.version < 11 && envelope.blueprint?.customSnapPoints?.Count > 512)
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                ValidateBlueprint(envelope.blueprint);
                if (envelope.requiredPrefabIds == null ||
                    !envelope.requiredPrefabIds.SequenceEqual(Prefabs(envelope.blueprint), StringComparer.Ordinal))
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                return envelope.blueprint;
            }
            catch (Exception exception) when (exception is SerializationException || exception is XmlException ||
                exception is ArgumentException || exception is FormatException || exception is OverflowException)
            { throw new InvalidDataException("$buildworks_import.document_invalid", exception); }
        }

        private static List<string> Prefabs(CompositeBlueprintStore.Blueprint blueprint) => blueprint.parts
            .Select(part => part.prefabName)
            .Concat(blueprint.parts.Where(part => part.foreignData?.format == Geometry.BlueprintForeignPartData.NativeItemStandFormat &&
                    !part.foreignData.contentsCleared)
                .Select(part => part.foreignData.info.Split(':')[0]))
            .Concat(blueprint.parts.Where(part => part.foreignData?.format == Geometry.BlueprintForeignPartData.NativeContainerFormat &&
                    !part.foreignData.contentsCleared)
                .SelectMany(part => part.foreignData.info.Split('|').Skip(1)))
            .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();

        private static void ValidateBlueprint(CompositeBlueprintStore.Blueprint blueprint)
        {
            if (!CompositeBlueprintStore.Valid(blueprint) || !SafeLabel(blueprint.name, 48) ||
                !SafeLabel(blueprint.category, 24) || blueprint.parts.Any(part => !SafeLabel(part.prefabName, 128) ||
                    part.prefabName.IndexOfAny(new[] { '/', '\\' }) >= 0 || !SafeLabel(part.stableId, 128) ||
                    !SafeLabel(part.displayName, 256)) || blueprint.groups.Any(group => !SafeLabel(group.stableId, 128) ||
                    !SafeLabel(group.name, 128)) || float.IsNaN(blueprint.previewYaw) || float.IsInfinity(blueprint.previewYaw) ||
                    float.IsNaN(blueprint.previewPitch) || blueprint.previewPitch < -80 || blueprint.previewPitch > 80 ||
                    float.IsNaN(blueprint.previewZoom) || blueprint.previewZoom < .5f || blueprint.previewZoom > 3)
                throw new InvalidDataException("$buildworks_import.document_invalid");
        }

        private static bool SafeLabel(string value, int limit) => !string.IsNullOrWhiteSpace(value) &&
            value.Length <= limit && value == value.Trim() && !value.Any(c => char.IsControl(c) || c == '<' || c == '>');

        // Reject unknown and duplicate fields rather than silently converting a newer document with data loss.
        private static void ValidateNode(XmlReader reader, Type expected, bool requireFields, bool allowSupportFields,
            bool allowSourceFields, bool allowForeignFields, bool allowContentsFields, bool allowAbsoluteScaleFields, bool allowPlacementImportFields, bool allowBaseScaleFields, bool allowMetadataFields, bool allowMaterialSeedFields, bool allowHiddenSources, bool sourceText = false, bool inventoryPackage = false, bool customSnapPoints = false)
        {
            string kind = reader.GetAttribute("type");
            if (kind == "null")
            {
                if (expected.IsValueType && Nullable.GetUnderlyingType(expected) == null) throw new InvalidDataException("$buildworks_import.document_invalid");
                reader.Skip(); return;
            }
            expected = Nullable.GetUnderlyingType(expected) ?? expected;
            if (expected.IsGenericType && expected.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (kind != "array") throw new InvalidDataException("$buildworks_import.document_invalid");
                Type item = expected.GetGenericArguments()[0];
                int count = 0, limit = customSnapPoints ? CompositeBlueprintStore.MaximumCustomSnapPoints :
                    item == typeof(CompositeBlueprintStore.Part) || item == typeof(string)
                    ? CompositeBlueprintStore.MaximumParts : item == typeof(CompositeBlueprintStore.Group) ||
                        item == typeof(CompositeBlueprintStore.Support) || item == typeof(CompositeBlueprintStore.SourceDocument) ? 64 : 512;
                if (reader.IsEmptyElement) { reader.Read(); return; }
                reader.ReadStartElement();
                while (reader.MoveToContent() == XmlNodeType.Element)
                {
                    if (reader.LocalName != "item" || ++count > limit) throw new InvalidDataException("$buildworks_import.document_invalid");
                    ValidateNode(reader, item, requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources);
                }
                reader.ReadEndElement();
                return;
            }
            if (!Fields.TryGetValue(expected, out Dictionary<string, Type> fields))
            {
                string scalarKind = expected == typeof(string) ? "string" : expected == typeof(bool) ? "boolean" : "number";
                if (kind != scalarKind) throw new InvalidDataException("$buildworks_import.document_invalid");
                string value = reader.ReadElementContentAsString();
                if (value.Length > (sourceText ? Geometry.BlueprintSourceDocument.MaximumBytes :
                    inventoryPackage ? Geometry.BlueprintForeignPartData.MaximumContainerBase64 : 32768))
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                return;
            }
            if (kind != "object") throw new InvalidDataException("$buildworks_import.document_invalid");
            if (reader.IsEmptyElement)
            {
                if (requireFields && RequiredFields[expected].Length != 0) throw new InvalidDataException("$buildworks_import.document_invalid");
                reader.Read(); return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            reader.ReadStartElement();
            while (reader.MoveToContent() == XmlNodeType.Element)
            {
                if (!fields.TryGetValue(reader.LocalName, out Type field) || !seen.Add(reader.LocalName) ||
                    !allowSupportFields && expected == typeof(CompositeBlueprintStore.Blueprint) &&
                        (reader.LocalName == "supports" || reader.LocalName == "placementSupportId") ||
                    !allowSourceFields && (expected == typeof(CompositeBlueprintStore.Blueprint) && reader.LocalName == "sourceDocuments" ||
                        expected == typeof(CompositeBlueprintStore.Part) && (reader.LocalName == "sourceDocumentId" || reader.LocalName == "sourceLine")) ||
                    !allowForeignFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "foreignData" ||
                    !allowContentsFields && expected == typeof(CompositeBlueprintStore.ForeignPartData) && reader.LocalName == "contentsCleared" ||
                    !allowAbsoluteScaleFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "scaleIsAbsolute" ||
                    !allowPlacementImportFields && expected == typeof(CompositeBlueprintStore.Blueprint) && reader.LocalName == "placementImportId" ||
                    !allowBaseScaleFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "baseScale" ||
                    !allowMetadataFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "metadata" ||
                    !allowMaterialSeedFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "materialSeed" ||
                    !allowHiddenSources && expected == typeof(CompositeBlueprintStore.LibraryData) && reader.LocalName == "hiddenSources")
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                bool textField = expected == typeof(CompositeBlueprintStore.SourceDocument) && reader.LocalName == "text";
                bool packageField = expected == typeof(CompositeBlueprintStore.ForeignPartData) && reader.LocalName == "zdo";
                bool snapField = expected == typeof(CompositeBlueprintStore.Blueprint) && reader.LocalName == "customSnapPoints";
                ValidateNode(reader, field, requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources, textField, packageField, snapField);
            }
            if (requireFields && RequiredFields[expected].Any(name => !seen.Contains(name)))
                throw new InvalidDataException("$buildworks_import.document_invalid");
            reader.ReadEndElement();
        }
    }
}

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
        internal const int Version = 24;

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
                typeof(CompositeBlueprintStore.SourceDocument), typeof(CompositeBlueprintStore.ForeignPartData), typeof(CompositeBlueprintStore.PrefabMetadata), typeof(CompositeBlueprintStore.LibraryOrigin), typeof(CompositeBlueprintStore.PeerOrigin),
                typeof(CompositeBlueprintStore.VectorData), typeof(CompositeBlueprintStore.QuaternionData),
                typeof(CompositeBlueprintStore.LibraryData), typeof(CompositeBlueprintStore.PeerReceipt) }
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
            bool allowBaseScaleFields, bool allowMetadataFields = true, bool allowMaterialSeedFields = true, bool allowHiddenSources = true, bool allowLibraryOriginFields = true, bool allowPrefabMigrationFields = true, bool allowPeerOriginFields = true, bool allowPeerReceiptFields = false, bool allowSourceFrameFields = false, bool allowSourceRecordFields = false, bool allowGroupSourceFields = false) => ValidateStructure(
            Encoding.UTF8.GetBytes(text), typeof(CompositeBlueprintStore.LibraryData), requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources, allowLibraryOriginFields, allowPrefabMigrationFields, allowPeerOriginFields, allowPeerReceiptFields, allowSourceFrameFields: allowSourceFrameFields, allowSourceRecordFields: allowSourceRecordFields, allowGroupSourceFields: allowGroupSourceFields);

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
            bool allowSourceFields = true, bool allowForeignFields = true, bool allowContentsFields = true, bool allowAbsoluteScaleFields = true, bool allowPlacementImportFields = false, bool allowBaseScaleFields = true, bool allowMetadataFields = true, bool allowMaterialSeedFields = true, bool allowHiddenSources = true, bool allowLibraryOriginFields = true, bool allowPrefabMigrationFields = true, bool allowPeerOriginFields = true, bool allowPeerReceiptFields = false, bool allowSourceFrameFields = true, bool allowSourceRecordFields = true, bool allowGroupSourceFields = true)
        {
            using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(bytes,
                new XmlDictionaryReaderQuotas
                {
                    MaxDepth = 16,
                    MaxStringContentLength = Geometry.KgBlueprint.MaximumBinaryTextLength,
                    MaxArrayLength = bytes.Length,
                    MaxBytesPerRead = 8192,
                    MaxNameTableCharCount = 16384
                }))
            {
                reader.MoveToContent();
                ValidateNode(reader, expected, requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources, allowLibraryOriginFields, allowPrefabMigrationFields, allowPeerOriginFields, allowPeerReceiptFields, allowSourceFrameFields: allowSourceFrameFields, allowSourceRecordFields: allowSourceRecordFields, allowGroupSourceFields: allowGroupSourceFields);
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

        internal static CompositeBlueprintStore.Blueprint Decode(string text) => DecodeWithVersion(text, out _);
        internal static CompositeBlueprintStore.Blueprint DecodeWithinBudget(string text, ref long sourceBudget) => DecodeCore(text, out _, ref sourceBudget);

        internal static CompositeBlueprintStore.Blueprint DecodeWithVersion(string text, out int formatVersion)
        {
            long sourceBudget = ExternalBlueprintLibrary.MaximumTotalBytes;
            return DecodeCore(text, out formatVersion, ref sourceBudget);
        }

        private static CompositeBlueprintStore.Blueprint DecodeCore(string text, out int formatVersion, ref long sourceBudget)
        {
            formatVersion = 0;
            int physicalBytes = text == null ? 0 : Encoding.UTF8.GetByteCount(text);
            if (text == null || physicalBytes > MaximumBytes || sourceBudget < 0 || sourceBudget > ExternalBlueprintLibrary.MaximumTotalBytes)
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
                        allowMaterialSeedFields: envelope.version >= 10, allowLibraryOriginFields: envelope.version >= 12, allowPrefabMigrationFields: envelope.version >= 13, allowPeerOriginFields: envelope.version >= 15, allowSourceFrameFields: envelope.version >= 17, allowSourceRecordFields: envelope.version >= 18, allowGroupSourceFields: envelope.version >= 23);
                if (envelope.blueprint != null)
                    envelope.blueprint.supports = envelope.blueprint.supports ?? new List<CompositeBlueprintStore.Support>();
                if (envelope.version < 8 && envelope.blueprint?.parts?.Count == 1)
                    throw new InvalidDataException("$buildworks_import.part_limit");
                if (envelope.version < 11 && envelope.blueprint?.customSnapPoints?.Count > 512)
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                if (envelope.version < 14 && HasNativeArmorState(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 16 && HasWorldCaptureOrigin(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 18 && HasOpenKeepSource(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 19 && HasKgData(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 20 && HasKgBinaryData(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 21 && HasBbpSource(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 22 && HasBetterBuildingJsonSource(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 23 && HasPlannerData(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                if (envelope.version < 24 && HasUnboundData(envelope.blueprint))
                    throw new InvalidDataException("$buildworks_store.unknown_version");
                ChargeBbpSources(envelope.blueprint, physicalBytes, ref sourceBudget);
                ValidateBlueprint(envelope.blueprint);
                if (envelope.requiredPrefabIds == null ||
                    !envelope.requiredPrefabIds.SequenceEqual(Prefabs(envelope.blueprint), StringComparer.Ordinal))
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                formatVersion = envelope.version;
                return envelope.blueprint;
            }
            catch (Exception exception) when (exception is SerializationException || exception is XmlException ||
                exception is ArgumentException || exception is FormatException || exception is OverflowException)
            { throw new InvalidDataException("$buildworks_import.document_invalid", exception); }
        }

        internal static bool HasWorldCaptureOrigin(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(source => source?.format == Geometry.BlueprintSourceDocument.WorldCaptureFormat) == true;

        internal static bool HasOpenKeepSource(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(source => source?.format == Geometry.OpenKeepBlueprint.Format) == true;
        internal static bool HasBbpSource(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(source => source?.format == Geometry.BetterBuildingBlueprint.BinaryFormat) == true;
        internal static bool HasPlannerData(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(s => s?.format == Geometry.PlannerBlueprint.Format) == true ||
            blueprint?.groups?.Any(g => g?.sourceDocumentId != null || g?.sourceLayerId != null) == true;
        internal static bool HasBetterBuildingJsonSource(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(source => source?.format == Geometry.BetterBuildingBlueprint.JsonFormat) == true;
        internal static bool HasUnboundData(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(source => source?.format == Geometry.UnboundBlueprint.Format) == true ||
            blueprint?.parts?.Any(part => part?.foreignData?.format == Geometry.UnboundBlueprint.Format) == true;

        // Serialized DTOs contain strings only. Charge before ToGeometry can inflate even a subsequently rejected source.
        private static void ChargeBbpSources(CompositeBlueprintStore.Blueprint blueprint, int physicalBytes, ref long budget)
        {
            if (!HasBbpSource(blueprint) && !HasBetterBuildingJsonSource(blueprint)) return;
            long extra = Math.Max(0, SourceBudgetBytes(blueprint) - physicalBytes);
            if (extra > budget) throw new InvalidDataException("$buildworks_import.file_limit");
            budget -= extra;
        }

        internal static void ValidateLibrarySourceBudget(IReadOnlyList<CompositeBlueprintStore.Blueprint> blueprints)
        {
            if (blueprints == null || !blueprints.Any(b => HasBbpSource(b) || HasBetterBuildingJsonSource(b))) return;
            long total = 0;
            foreach (var blueprint in blueprints)
            {
                total += SourceBudgetBytes(blueprint);
                if (total > ExternalBlueprintLibrary.MaximumTotalBytes) throw new InvalidDataException("$buildworks_import.file_limit");
            }
        }

        private static long SourceBudgetBytes(CompositeBlueprintStore.Blueprint blueprint)
        {
            if (blueprint?.sourceDocuments == null) return 0;
            if (blueprint.sourceDocuments.Count > Geometry.BlueprintSourceDocument.MaximumSources) throw new InvalidDataException("$buildworks_import.document_invalid");
            long total = 0;
            foreach (var source in blueprint.sourceDocuments)
            {
                if (source == null || source.text == null) throw new InvalidDataException("$buildworks_import.document_invalid");
                bool bbp = source.format == Geometry.BetterBuildingBlueprint.BinaryFormat;
                if (bbp || source.format == Geometry.KgBlueprint.BinaryFormat)
                {
                    if (source.text.Length > Geometry.KgBlueprint.MaximumBinaryTextLength) throw new InvalidDataException("$buildworks_import.file_limit");
                    byte[] bytes = Convert.FromBase64String(source.text);
                    if (bytes.Length > Geometry.BlueprintSourceDocument.MaximumBytes || bbp && bytes.Length < 48) throw new InvalidDataException("$buildworks_import.file_limit");
                    int raw = bytes.Length;
                    if (bbp)
                    {
                        raw = bytes[8] | bytes[9] << 8 | bytes[10] << 16 | bytes[11] << 24;
                        if (raw < 1 || raw > Geometry.BetterBuildingBlueprint.MaximumRawBytes) throw new InvalidDataException("$buildworks_import.file_limit");
                    }
                    total += Math.Max(bytes.Length, raw);
                }
                else
                {
                    int bytes = new UTF8Encoding(false, true).GetByteCount(source.text);
                    if (bytes > Geometry.BlueprintSourceDocument.MaximumBytes) throw new InvalidDataException("$buildworks_import.file_limit");
                    total += bytes;
                }
                if (total > Geometry.BlueprintSourceDocument.MaximumTotalBytes) throw new InvalidDataException("$buildworks_import.file_limit");
            }
            return total;
        }
        internal static bool HasKgData(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(source => Geometry.KgBlueprint.IsFormat(source?.format)) == true ||
            blueprint?.parts?.Any(part => Geometry.KgBlueprint.IsFormat(part?.foreignData?.format)) == true;
        internal static bool HasKgBinaryData(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.sourceDocuments?.Any(source => source?.format == Geometry.KgBlueprint.BinaryFormat) == true ||
            blueprint?.parts?.Any(part => part?.foreignData?.format == Geometry.KgBlueprint.BinaryFormat) == true;

        private static List<string> Prefabs(CompositeBlueprintStore.Blueprint blueprint) => blueprint.parts
            .Select(part => part.prefabName)
            .Concat(blueprint.parts.Where(part => part.foreignData?.format == Geometry.BlueprintForeignPartData.NativeItemStandFormat &&
                    !part.foreignData.contentsCleared)
                .Select(part => part.foreignData.info.Split(':')[0]))
            .Concat(blueprint.parts.Where(part => part.foreignData?.format == Geometry.BlueprintForeignPartData.NativeContainerFormat &&
                    !part.foreignData.contentsCleared)
                .SelectMany(part => part.foreignData.info.Split('|').Skip(1)))
            .Concat(blueprint.parts.Where(part => part.foreignData?.format == Geometry.BlueprintForeignPartData.NativeArmorStandFormat &&
                    !part.foreignData.contentsCleared)
                .SelectMany(part => Geometry.BlueprintArmorStandState.Read(part.foreignData.ToGeometry()).Slots)
                .Where(slot => slot.ItemId != 0 && slot.Prefab != null && slot.Prefab.GetStableHashCode() == slot.ItemId).Select(slot => slot.Prefab))
            .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();

        internal static bool HasNativeArmorState(CompositeBlueprintStore.Blueprint blueprint) =>
            blueprint?.parts?.Any(part => part?.foreignData?.format == Geometry.BlueprintForeignPartData.NativeArmorStandFormat) == true;

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
            bool allowSourceFields, bool allowForeignFields, bool allowContentsFields, bool allowAbsoluteScaleFields, bool allowPlacementImportFields, bool allowBaseScaleFields, bool allowMetadataFields, bool allowMaterialSeedFields, bool allowHiddenSources, bool allowLibraryOriginFields, bool allowPrefabMigrationFields, bool allowPeerOriginFields, bool allowPeerReceiptFields, bool sourceText = false, bool inventoryPackage = false, bool customSnapPoints = false, bool allowSourceFrameFields = true, bool allowSourceRecordFields = true, bool allowGroupSourceFields = true)
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
                    item == typeof(CompositeBlueprintStore.PeerReceipt) ? CompositeBlueprintStore.MaximumPeerReceipts :
                    item == typeof(CompositeBlueprintStore.LibraryOrigin) ? CompositeBlueprintStore.MaximumLibraryOrigins :
                    item == typeof(CompositeBlueprintStore.PeerOrigin) ? Geometry.BlueprintPeerOrigin.MaximumCount :
                    item == typeof(CompositeBlueprintStore.Part) || item == typeof(string)
                    ? CompositeBlueprintStore.MaximumParts : item == typeof(CompositeBlueprintStore.Group) ||
                        item == typeof(CompositeBlueprintStore.Support) || item == typeof(CompositeBlueprintStore.SourceDocument) ? 64 : 512;
                if (reader.IsEmptyElement) { reader.Read(); return; }
                reader.ReadStartElement();
                while (reader.MoveToContent() == XmlNodeType.Element)
                {
                    if (reader.LocalName != "item" || ++count > limit) throw new InvalidDataException("$buildworks_import.document_invalid");
                    ValidateNode(reader, item, requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources, allowLibraryOriginFields, allowPrefabMigrationFields, allowPeerOriginFields, allowPeerReceiptFields, allowSourceFrameFields: allowSourceFrameFields, allowSourceRecordFields: allowSourceRecordFields, allowGroupSourceFields: allowGroupSourceFields);
                }
                reader.ReadEndElement();
                return;
            }
            if (!Fields.TryGetValue(expected, out Dictionary<string, Type> fields))
            {
                string scalarKind = expected == typeof(string) ? "string" : expected == typeof(bool) ? "boolean" : "number";
                if (kind != scalarKind) throw new InvalidDataException("$buildworks_import.document_invalid");
                string value = reader.ReadElementContentAsString();
                // SourceDocument validates the format-specific raw/encoded quota after structural validation.
                if (value.Length > (sourceText ? Geometry.KgBlueprint.MaximumBinaryTextLength :
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
                    !allowHiddenSources && expected == typeof(CompositeBlueprintStore.LibraryData) && reader.LocalName == "hiddenSources" ||
                    !allowPeerReceiptFields && expected == typeof(CompositeBlueprintStore.LibraryData) && reader.LocalName == "peerReceipts" ||
                    !allowLibraryOriginFields && expected == typeof(CompositeBlueprintStore.Blueprint) && reader.LocalName == "libraryOrigins" ||
                    !allowPeerOriginFields && expected == typeof(CompositeBlueprintStore.Blueprint) && reader.LocalName == "peerOrigins" ||
                    !allowPrefabMigrationFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "originalPrefabName" ||
                    !allowSourceFrameFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "sourceFrameApplied" ||
                    !allowSourceRecordFields && expected == typeof(CompositeBlueprintStore.Part) && reader.LocalName == "sourceRecord" ||
                    !allowGroupSourceFields && expected == typeof(CompositeBlueprintStore.Group) &&
                        (reader.LocalName == "sourceDocumentId" || reader.LocalName == "sourceLayerId"))
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                bool textField = expected == typeof(CompositeBlueprintStore.SourceDocument) && reader.LocalName == "text";
                bool packageField = expected == typeof(CompositeBlueprintStore.ForeignPartData) && reader.LocalName == "zdo";
                bool snapField = expected == typeof(CompositeBlueprintStore.Blueprint) && reader.LocalName == "customSnapPoints";
                ValidateNode(reader, field, requireFields, allowSupportFields, allowSourceFields, allowForeignFields, allowContentsFields, allowAbsoluteScaleFields, allowPlacementImportFields, allowBaseScaleFields, allowMetadataFields, allowMaterialSeedFields, allowHiddenSources, allowLibraryOriginFields, allowPrefabMigrationFields, allowPeerOriginFields, allowPeerReceiptFields, textField, packageField, snapField, allowSourceFrameFields, allowSourceRecordFields, allowGroupSourceFields);
            }
            if (requireFields && RequiredFields[expected].Any(name => !seen.Contains(name)))
                throw new InvalidDataException("$buildworks_import.document_invalid");
            reader.ReadEndElement();
        }
    }
}

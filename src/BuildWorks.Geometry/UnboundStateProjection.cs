using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace OstrixMods.BuildWorks.Geometry
{
    // Ephemeral host facts, never serialized permissions or a replacement for final native validation.
    public sealed class UnboundImportContext
    {
        public UnboundImportContext(BlueprintPrefabMetadata metadata, bool syncInitialScale, bool ordinary,
            bool sign, bool materialSeed, (int Width, int Height)? containerGrid = null,
            (int PrefabHash, bool Persistent, bool Distant, byte ObjectType)? nativeHeader = null,
            IEnumerable<UnboundItemStandItem> itemStandItems = null)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            if (containerGrid.HasValue && (containerGrid.Value.Width < 1 || containerGrid.Value.Width > 256 ||
                containerGrid.Value.Height < 1 || containerGrid.Value.Height > 256)) throw new ArgumentException("Invalid container grid.");
            SyncInitialScale = syncInitialScale; Ordinary = ordinary; Sign = sign; MaterialSeed = materialSeed; ContainerGrid = containerGrid;
            if (nativeHeader.HasValue && (nativeHeader.Value.PrefabHash != KgBlueprint.StablePrefabHash(metadata.PrefabId) ||
                nativeHeader.Value.ObjectType > 3)) throw new ArgumentException("Invalid native prefab header.");
            NativeHeader = nativeHeader;
            if (itemStandItems != null)
            {
                if (ordinary || containerGrid.HasValue) throw new ArgumentException("Mixed item-stand capabilities.");
                var items = itemStandItems.ToArray();
                if (items.Any(item => item == null)) throw new ArgumentException("Missing item-stand catalog facts.");
                ItemStandItems = new ReadOnlyDictionary<int, UnboundItemStandItem>(
                    items.ToDictionary(item => KgBlueprint.StablePrefabHash(item.Name)));
            }
        }
        public BlueprintPrefabMetadata Metadata { get; }
        public bool SyncInitialScale { get; }
        public bool Ordinary { get; }
        public bool Sign { get; }
        public bool MaterialSeed { get; }
        public (int Width, int Height)? ContainerGrid { get; }
        public (int PrefabHash, bool Persistent, bool Distant, byte ObjectType)? NativeHeader { get; }
        public IReadOnlyDictionary<int, UnboundItemStandItem> ItemStandItems { get; }
    }

    public static class UnboundStateProjection
    {
        private static int Hash(string name) => KgBlueprint.StablePrefabHash(name);
        private static readonly int Text = Hash("text"), Author = Hash("author"), Seed = Hash("RandMatSeed"),
            Items = Hash("items"), Scale = Hash("scale"), Scalar = Hash("scaleScalar"), NoSupport = Hash("vt_bp_nosupport");
        private static readonly float VectorZeroSquared = BitConverter.ToSingle(BitConverter.GetBytes(0x2EDBE6FE), 0);
        private static readonly Dictionary<int, Type> Known = new Dictionary<int, Type> {
            [Text] = typeof(string), [Author] = typeof(string), [Seed] = typeof(int), [Items] = typeof(byte[]),
            [Scale] = typeof(Point3), [Scalar] = typeof(float), [NoSupport] = typeof(int),
            [Hash("creator")] = typeof(long), [Hash("creatorIndex")] = typeof(int),
            [Hash("health")] = typeof(float), [Hash("support")] = typeof(float),
            [Hash("BuildWorks_ExactRotation_v1")] = typeof(Rotation3),
            [Hash("BuildWorks_ExactRotationWitness_v1")] = typeof(Rotation3),
            [Hash("ostrix_buildworks_project_operation_v1")] = typeof(string),
            [Hash("ostrix_buildworks_project_receipt_v1")] = typeof(byte[])
        };

        public sealed class Projection
        {
            internal Projection(Point3 scale, string text, int? seed, BlueprintForeignPartData contents)
            { RootScale = scale; SignText = text; MaterialSeed = seed; Contents = contents; }
            public Point3 RootScale { get; }
            public string SignText { get; }
            public int? MaterialSeed { get; }
            public BlueprintForeignPartData Contents { get; }
        }

        public static bool TryProject(UnboundBlueprint.Piece source, UnboundImportContext context,
            Func<int, string> resolveItem, out Projection projection)
        {
            projection = null;
            if (source == null || context == null || context.Metadata.PrefabId != source.PrefabName ||
                !FullyKnown(source.State, context.ItemStandItems != null) ||
                !context.Ordinary && !context.ContainerGrid.HasValue && context.ItemStandItems == null) return false;
            var state = source.State;
            if (!SupportsFields(state, context) ||
                !TryRootScale(state, context.Metadata.PrefabScale, out var root)) return false;
            state.TryGet<string>(Text, out var text);
            int? seed = state.TryGet<int>(Seed, out var value) ? value : (int?)null;
            BlueprintForeignPartData contents = null;
            if (state.TryGet<byte[]>(Items, out var bytes) &&
                !BlueprintContainerInventory.TryConvertNative(Convert.ToBase64String(bytes), context.ContainerGrid.Value.Width,
                    context.ContainerGrid.Value.Height, resolveItem, out contents)) return false;
            if (context.ItemStandItems != null)
            {
                if (!BlueprintItemStandContents.TryConvertNative(state, resolveItem, out contents)) return false;
                if (contents != null && (!context.ItemStandItems.TryGetValue(Hash(contents.Info.Split(':')[0]), out var item) ||
                    item.Name != contents.Info.Split(':')[0])) return false;
            }
            // Creator, lifetime, health/support and old receipts remain only in immutable source evidence.
            projection = new Projection(root, text, seed, contents); return true;
        }

        public static bool Matches(UnboundBlueprint.Piece source, BlueprintPrefabMetadata metadata, Point3? basis,
            bool absoluteScale, string text, int? seed, BlueprintForeignPartData contents)
        {
            if (source == null || metadata == null || metadata.PrefabId != source.PrefabName || absoluteScale || !basis.HasValue ||
                !FullyKnown(source.State, contents?.Format == BlueprintForeignPartData.NativeItemStandFormat || HasItemStandFields(source.State)) ||
                !TryRootScale(source.State, metadata.PrefabScale, out var expected) ||
                basis.Value.X != expected.X || basis.Value.Y != expected.Y || basis.Value.Z != expected.Z ||
                !BlueprintEditorPart.IsValidSignText(text) || seed.HasValue && seed.Value < 0 ||
                source.State.HasHash(Text) && text == null || source.State.HasHash(Seed) && !seed.HasValue) return false;
            if (contents?.Format == BlueprintForeignPartData.NativeItemStandFormat || HasItemStandFields(source.State))
            {
                string name = contents?.Info.Split(':')[0];
                if (!BlueprintItemStandContents.TryConvertNative(source.State, id => name != null && Hash(name) == id ? name : null, out var original)) return false;
                if (original == null) return contents == null;
                return contents?.Format == BlueprintForeignPartData.NativeItemStandFormat && contents.Zdo == original.Zdo &&
                    contents.Info.Split(':').Take(3).SequenceEqual(original.Info.Split(':').Take(3)) &&
                    BlueprintItemStandContents.TryRead(contents, Hash, out _);
            }
            if (!source.State.TryGet<byte[]>(Items, out var bytes)) return contents == null;
            return contents?.Format == BlueprintForeignPartData.NativeContainerFormat &&
                contents.Zdo == Convert.ToBase64String(bytes) &&
                BlueprintContainerInventory.TryRead(contents, Hash, out _);
        }

        // Rebuild from working properties, never replay archived authority or cleared inventory bytes.
        public static bool TryEncode(UnboundBlueprint.Piece source, UnboundImportContext context, Point3 rootScale,
            string text, int? seed, BlueprintForeignPartData contents, out string packet)
        {
            packet = null;
            if (context?.NativeHeader == null || !context.Ordinary && !context.ContainerGrid.HasValue && context.ItemStandItems == null ||
                !BlueprintEditorPart.IsValidSignText(text) || text != null && !context.Sign ||
                seed.HasValue && (!context.MaterialSeed || seed.Value < 0) ||
                !Positive(rootScale.X) || !Positive(rootScale.Y) || !Positive(rootScale.Z)) return false;
            if (context.ItemStandItems != null && contents != null && contents.Format != BlueprintForeignPartData.NativeItemStandFormat) return false;
            var header = context.NativeHeader.Value;
            // Instance lifetime/network flags are provenance; export uses the current prefab header.
            if (source != null && (source.PrefabName != context.Metadata.PrefabId || !FullyKnown(source.State, context.ItemStandItems != null) ||
                !SupportsFields(source.State, context) ||
                source.State.HasHash(Text) && text == null || source.State.HasHash(Seed) && !seed.HasValue ||
                source.State.HasHash(Items) && contents == null)) return false;
            if (context.ItemStandItems != null && source != null &&
                !Matches(source, context.Metadata, TryRootScale(source.State, context.Metadata.PrefabScale, out var sourceScale) ? sourceScale : (Point3?)null,
                    false, text, seed, contents)) return false;
            try
            {
                var updates = new List<KeyValuePair<int, object>>();
                if (text != null) updates.Add(new KeyValuePair<int, object>(Text, text));
                if (seed.HasValue) updates.Add(new KeyValuePair<int, object>(Seed, seed.Value));
                Point3 prefab = context.Metadata.PrefabScale;
                if (rootScale.X != prefab.X || rootScale.Y != prefab.Y || rootScale.Z != prefab.Z)
                {
                    if (!context.SyncInitialScale) return false;
                    updates.Add(new KeyValuePair<int, object>(Scale, rootScale));
                    if (rootScale.X == rootScale.Y && rootScale.Y == rootScale.Z)
                        updates.Add(new KeyValuePair<int, object>(Scalar, (float)rootScale.X));
                }
                if (context.ContainerGrid.HasValue)
                {
                    // Pinned Inventory.Save v110 writes Int32 version + UInt16 count (HostContract).
                    byte[] inventory = { 110, 0, 0, 0, 0, 0 };
                    if (contents != null)
                    {
                        var grid = context.ContainerGrid.Value;
                        if (!BlueprintContainerInventory.TryRead(contents, Hash, out _)) return false;
                        var dimensions = contents.Info.Split('|')[0].Split(':');
                        if (int.Parse(dimensions[0], CultureInfo.InvariantCulture) != grid.Width ||
                            int.Parse(dimensions[1], CultureInfo.InvariantCulture) != grid.Height) return false;
                        if (!contents.ContentsCleared) inventory = Convert.FromBase64String(contents.Zdo);
                    }
                    updates.Add(new KeyValuePair<int, object>(Items, inventory));
                }
                else if (context.ItemStandItems != null)
                {
                    int itemId = 0;
                    if (contents != null && !contents.ContentsCleared)
                    {
                        if (!context.ItemStandItems.TryGetValue(Hash(contents.Info.Split(':')[0]), out var item) || !item.Supports(contents)) return false;
                        string[] fields = contents.Info.Split(':'); itemId = Hash(item.Name);
                        updates.Add(new KeyValuePair<int, object>(Hash("variant"), int.Parse(fields[1], CultureInfo.InvariantCulture)));
                        updates.Add(new KeyValuePair<int, object>(Hash("quality"), int.Parse(fields[2], CultureInfo.InvariantCulture)));
                        updates.Add(new KeyValuePair<int, object>(Hash("type"), int.Parse(fields[3], CultureInfo.InvariantCulture)));
                        updates.Add(new KeyValuePair<int, object>(Hash("itemData"), Convert.FromBase64String(contents.Zdo)));
                    }
                    updates.Add(new KeyValuePair<int, object>(Hash("item"), itemId));
                }
                else if (contents != null) return false;
                string written = BlueprintForeignZdo.CreateNative(header.PrefabHash, header.Persistent,
                    header.Distant, header.ObjectType).WriteNative(updates);
                if (!BlueprintForeignZdo.TryReadNative(written, header.PrefabHash, out var result) ||
                    !TryRootScale(result, prefab, out var actual) || actual.X != rootScale.X ||
                    actual.Y != rootScale.Y || actual.Z != rootScale.Z || !FullyKnown(result, context.ItemStandItems != null)) return false;
                packet = written;
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is System.IO.IOException)
            { return false; }
        }

        private static bool SupportsFields(BlueprintForeignZdo state, UnboundImportContext context) =>
            !((state.HasHash(Text) || state.HasHash(Author)) && !context.Sign ||
                state.HasHash(Seed) && !context.MaterialSeed ||
                (state.HasHash(Scale) || state.HasHash(Scalar)) && !context.SyncInitialScale ||
                state.HasHash(Items) != context.ContainerGrid.HasValue || HasItemStandFields(state) && context.ItemStandItems == null);

        private static bool HasItemStandFields(BlueprintForeignZdo state) => state.Keys.Any(field => BlueprintItemStandContents.NativeFields.ContainsKey(field.Key));
        private static bool FullyKnown(BlueprintForeignZdo state, bool itemStand = false)
        {
            if (state.ConnectionType.HasValue || state.Keys.Any(key =>
                !(Known.TryGetValue(key.Key, out var type) && type == key.Value || itemStand &&
                    BlueprintItemStandContents.NativeFields.TryGetValue(key.Key, out type) && type == key.Value))) return false;
            if (state.TryGet<int>(NoSupport, out var bypass) && bypass != 0 && bypass != 1) return false;
            if (state.TryGet<string>(Text, out var text) && !BlueprintEditorPart.IsValidSignText(text)) return false;
            return !state.TryGet<int>(Seed, out var seed) || seed >= 0;
        }

        private static bool TryRootScale(BlueprintForeignZdo state, Point3 prefab, out Point3 root)
        {
            var result = prefab;
            if (state.TryGet<Point3>(Scale, out var vector))
            {
                float x = (float)vector.X, y = (float)vector.Y, z = (float)vector.Z;
                // Pinned Unity operator uses Single squared distance and strict <, not exact Vector3.Equals.
                if (!(x * x + y * y + z * z < VectorZeroSquared)) result = vector;
                else ApplyScalar();
            }
            else ApplyScalar();
            root = result;
            return Positive(root.X) && Positive(root.Y) && Positive(root.Z);

            void ApplyScalar()
            {
                if (state.TryGet<float>(Scalar, out var scalar) && !((float)prefab.X).Equals(scalar))
                    result = new Point3(scalar, scalar, scalar);
            }
        }
        private static bool Positive(double value) => GeometryMath.IsFinite(value) && value > 0 && value <= float.MaxValue;
    }
}

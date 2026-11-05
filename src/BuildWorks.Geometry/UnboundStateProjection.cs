using System;
using System.Collections.Generic;
using System.Linq;

namespace OstrixMods.BuildWorks.Geometry
{
    // Ephemeral host facts, never serialized permissions or a replacement for final native validation.
    public sealed class UnboundImportContext
    {
        public UnboundImportContext(BlueprintPrefabMetadata metadata, bool syncInitialScale, bool ordinary,
            bool sign, bool materialSeed, (int Width, int Height)? containerGrid = null)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            if (containerGrid.HasValue && (containerGrid.Value.Width < 1 || containerGrid.Value.Width > 256 ||
                containerGrid.Value.Height < 1 || containerGrid.Value.Height > 256)) throw new ArgumentException("Invalid container grid.");
            SyncInitialScale = syncInitialScale; Ordinary = ordinary; Sign = sign; MaterialSeed = materialSeed; ContainerGrid = containerGrid;
        }
        public BlueprintPrefabMetadata Metadata { get; }
        public bool SyncInitialScale { get; }
        public bool Ordinary { get; }
        public bool Sign { get; }
        public bool MaterialSeed { get; }
        public (int Width, int Height)? ContainerGrid { get; }
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
                !FullyKnown(source.State) || !context.Ordinary && !context.ContainerGrid.HasValue) return false;
            var state = source.State;
            if ((state.HasHash(Text) || state.HasHash(Author)) && !context.Sign ||
                state.HasHash(Seed) && !context.MaterialSeed ||
                (state.HasHash(Scale) || state.HasHash(Scalar)) && !context.SyncInitialScale ||
                state.HasHash(Items) != context.ContainerGrid.HasValue ||
                !TryRootScale(state, context.Metadata.PrefabScale, out var root)) return false;
            state.TryGet<string>(Text, out var text);
            int? seed = state.TryGet<int>(Seed, out var value) ? value : (int?)null;
            BlueprintForeignPartData contents = null;
            if (state.TryGet<byte[]>(Items, out var bytes) &&
                !BlueprintContainerInventory.TryConvertNative(Convert.ToBase64String(bytes), context.ContainerGrid.Value.Width,
                    context.ContainerGrid.Value.Height, resolveItem, out contents)) return false;
            // Creator, lifetime, health/support and old receipts remain only in immutable source evidence.
            projection = new Projection(root, text, seed, contents); return true;
        }

        public static bool Matches(UnboundBlueprint.Piece source, BlueprintPrefabMetadata metadata, Point3? basis,
            bool absoluteScale, string text, int? seed, BlueprintForeignPartData contents)
        {
            if (source == null || metadata == null || metadata.PrefabId != source.PrefabName || absoluteScale || !basis.HasValue ||
                !FullyKnown(source.State) || !TryRootScale(source.State, metadata.PrefabScale, out var expected) ||
                basis.Value.X != expected.X || basis.Value.Y != expected.Y || basis.Value.Z != expected.Z ||
                !BlueprintEditorPart.IsValidSignText(text) || seed.HasValue && seed.Value < 0 ||
                source.State.HasHash(Text) && text == null || source.State.HasHash(Seed) && !seed.HasValue) return false;
            if (!source.State.TryGet<byte[]>(Items, out var bytes)) return contents == null;
            return contents?.Format == BlueprintForeignPartData.NativeContainerFormat &&
                contents.Zdo == Convert.ToBase64String(bytes) &&
                BlueprintContainerInventory.TryRead(contents, Hash, out _);
        }

        private static bool FullyKnown(BlueprintForeignZdo state)
        {
            if (state.ConnectionType.HasValue || state.Keys.Any(key => !Known.TryGetValue(key.Key, out var type) || type != key.Value)) return false;
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

using System;
using System.Collections.Generic;
using System.IO;

namespace OstrixMods.BuildWorks.Geometry
{
    // Only finite piece-local coordinates. No foreign ZDO data or executable object state.
    public static class BlueprintSnapPointCodec
    {
        public const int MaximumPoints = 512;
        private const int Magic = 0x31535742; // BWS1, little endian

        public static byte[] Encode(IReadOnlyList<Point3> points)
        {
            if (points == null || points.Count > MaximumPoints) throw new ArgumentException("Invalid snap-point count.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(points.Count);
                foreach (Point3 point in points)
                {
                    Validate(point);
                    writer.Write((float)point.X); writer.Write((float)point.Y); writer.Write((float)point.Z);
                }
                return stream.ToArray();
            }
        }

        public static Point3[] Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8 || bytes.Length > 8 + MaximumPoints * 12)
                throw new ArgumentException("Invalid snap-point package.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != Magic) throw new ArgumentException("Unknown snap-point package.");
                int count = reader.ReadInt32();
                if (count < 0 || count > MaximumPoints || bytes.Length != 8 + count * 12)
                    throw new ArgumentException("Invalid snap-point count.");
                var points = new Point3[count];
                for (int index = 0; index < count; ++index)
                {
                    points[index] = new Point3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Validate(points[index]);
                }
                return points;
            }
        }

        private static void Validate(Point3 point)
        {
            if (!GeometryMath.IsFinite(point.X) || !GeometryMath.IsFinite(point.Y) || !GeometryMath.IsFinite(point.Z) ||
                Math.Abs(point.X) > 1000000 || Math.Abs(point.Y) > 1000000 || Math.Abs(point.Z) > 1000000)
                throw new ArgumentException("Invalid piece-local snap point.");
        }
    }
}

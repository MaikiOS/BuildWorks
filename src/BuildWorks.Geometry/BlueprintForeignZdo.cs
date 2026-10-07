using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Read-only DataEntry wire format, NOT native ZDO.Deserialize. Exact source stays archived.
    // Schema: valheim-expand_world_data/ExpandWorldData/service/data/DataEntry.cs Load(ZPackage).
    public sealed class BlueprintForeignZdo
    {
        private readonly Dictionary<KeyValuePair<int, Type>, object> values = new Dictionary<KeyValuePair<int, Type>, object>();
        private BlueprintForeignZdo() { }
        public IEnumerable<KeyValuePair<int, Type>> Keys => values.Keys;
        public int Count => values.Count;
        public byte? ConnectionType { get; private set; }
        public int ConnectionHash { get; private set; }
        public bool? Persistent { get; private set; }
        public bool? Distant { get; private set; }
        public byte? Priority { get; private set; }

        // Exact type only: object cannot expose the private byte-array storage.
        public bool TryGet<T>(int key, out T value)
        {
            value = default;
            if (!values.TryGetValue(new KeyValuePair<int, Type>(key, typeof(T)), out object stored)) return false;
            value = (T)(stored is byte[] bytes ? bytes.Clone() : stored);
            return true;
        }

        public bool HasHash(int key) => values.Keys.Any(value => value.Key == key);

        public static bool TryRead(string base64, out BlueprintForeignZdo data)
        {
            data = null;
            if (base64 == null || base64.Length > 32768) return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                if (bytes.Length < 4 || Convert.ToBase64String(bytes) != base64) return false;
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    int mask = reader.ReadInt32();
                    if ((mask & ~4063) != 0) return false; // Bit32 is not defined by DataEntry.Load.
                    var result = new BlueprintForeignZdo();
                    ReadSection(1, () => (object)Finite(reader.ReadSingle()));
                    ReadSection(2, () => new Point3(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()), Finite(reader.ReadSingle())));
                    ReadSection(4, () => new Rotation3(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()),
                        Finite(reader.ReadSingle()), Finite(reader.ReadSingle())));
                    ReadSection(8, () => reader.ReadInt32());
                    // Longs precede strings in both historical and current DataEntry.
                    ReadSection(64, () => reader.ReadInt64());
                    ReadSection(16, () => BlueprintContainerInventory.ReadString(reader));
                    ReadSection(128, () =>
                    {
                        int length = reader.ReadInt32();
                        if (length < 0 || length > stream.Length - stream.Position) throw new InvalidDataException();
                        return reader.ReadBytes(length);
                    });
                    if ((mask & 256) != 0) { result.ConnectionType = reader.ReadByte(); result.ConnectionHash = reader.ReadInt32(); }
                    if ((mask & 512) != 0) result.Persistent = ReadBool();
                    if ((mask & 1024) != 0) result.Distant = ReadBool();
                    if ((mask & 2048) != 0) result.Priority = reader.ReadByte();
                    if (stream.Position != stream.Length) return false;
                    data = result;
                    return true;

                    void ReadSection(int bit, Func<object> read)
                    {
                        if ((mask & bit) == 0) return;
                        int count = reader.ReadByte();
                        for (int index = 0; index < count; ++index)
                        {
                            int key = reader.ReadInt32();
                            object value = read();
                            var typedKey = new KeyValuePair<int, Type>(key, value.GetType());
                            if (result.values.ContainsKey(typedKey)) throw new InvalidDataException();
                            result.values.Add(typedKey, value);
                        }
                    }
                    bool ReadBool()
                    {
                        byte value = reader.ReadByte();
                        if (value > 1) throw new InvalidDataException();
                        return value == 1;
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is FormatException || error is ArgumentException)
            { return false; }
        }

        private static float Finite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException();
            return value;
        }
    }
}

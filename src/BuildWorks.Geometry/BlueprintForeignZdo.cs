using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Read-only DataEntry/KG/native Serialize wire formats; never native ZDO.Deserialize.
    // Schema: valheim-expand_world_data/ExpandWorldData/service/data/DataEntry.cs Load(ZPackage).
    public sealed class BlueprintForeignZdo
    {
        private readonly Dictionary<KeyValuePair<int, Type>, object> values = new Dictionary<KeyValuePair<int, Type>, object>();
        private bool kgDialect;
        private BlueprintForeignZdo() { }
        public IEnumerable<KeyValuePair<int, Type>> Keys => values.Keys;
        public int Count => values.Count;
        public byte? ConnectionType { get; private set; }
        public int ConnectionHash { get; private set; }
        public bool? Persistent { get; private set; }
        public bool? Distant { get; private set; }
        public byte? Priority { get; private set; }
        public ushort? NativeFlags { get; private set; }
        public int? NativePrefabHash { get; private set; }
        public byte? NativeObjectType => NativeFlags.HasValue ? (byte?)((NativeFlags.Value >> 10) & 3) : null;
        public Point3? NativeRotationEuler { get; private set; }
        public long? NativeConnectionUserId { get; private set; }
        public uint? NativeConnectionId { get; private set; }

        // Exact type only: object cannot expose the private byte-array storage.
        public bool TryGet<T>(int key, out T value)
        {
            value = default;
            if (!values.TryGetValue(new KeyValuePair<int, Type>(key, typeof(T)), out object stored)) return false;
            value = (T)(stored is byte[] bytes ? bytes.Clone() : stored);
            return true;
        }

        public bool HasHash(int key) => values.Keys.Any(value => value.Key == key);

        public static BlueprintForeignZdo CreateNative(int prefabHash, bool persistent, bool distant, byte objectType)
        {
            if (objectType > 3) throw new ArgumentOutOfRangeException(nameof(objectType));
            return new BlueprintForeignZdo {
                NativeFlags = (ushort)((persistent ? 256 : 0) | (distant ? 512 : 0) | (objectType << 10)),
                NativePrefabHash = prefabHash, Persistent = persistent, Distant = distant
            };
        }

        // Unbound38 stores the pinned game's ZDO.Serialize projection, not Save chunks or DataEntry.
        public static bool TryReadNative(string base64, int expectedPrefabHash, out BlueprintForeignZdo data)
        {
            data = null;
            if (base64 == null || base64.Length > BlueprintForeignPartData.MaximumContainerBase64) return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                if (bytes.Length < 6 || bytes.Length > BlueprintForeignPartData.MaximumContainerBytes ||
                    Convert.ToBase64String(bytes) != base64) return false;
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    ushort flags = reader.ReadUInt16();
                    int prefab = reader.ReadInt32();
                    // SmallPosition exists in the enum but this Serialize never emits it.
                    if ((flags & ~8191) != 0 || prefab != expectedPrefabHash) return false;
                    var result = new BlueprintForeignZdo { NativeFlags = flags, NativePrefabHash = prefab,
                        Persistent = (flags & 256) != 0, Distant = (flags & 512) != 0 };
                    if ((flags & 4096) != 0) result.NativeRotationEuler = Vector();
                    if ((flags & 1) != 0)
                    {
                        byte connection = reader.ReadByte();
                        if (connection == 0) return false;
                        result.ConnectionType = connection;
                        result.NativeConnectionUserId = reader.ReadInt64();
                        result.NativeConnectionId = reader.ReadUInt32();
                    }
                    Section(2, () => (object)Finite(reader.ReadSingle()));
                    Section(4, () => Vector());
                    Section(8, () => new Rotation3(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()),
                        Finite(reader.ReadSingle()), Finite(reader.ReadSingle())));
                    Section(16, () => reader.ReadInt32());
                    Section(32, () => reader.ReadInt64());
                    Section(64, () => BlueprintContainerInventory.ReadString(reader, (int)(stream.Length - stream.Position)));
                    Section(128, () =>
                    {
                        int count = reader.ReadInt32();
                        if (count < 0 || count > stream.Length - stream.Position) throw new InvalidDataException();
                        return reader.ReadBytes(count);
                    });
                    if (stream.Position != stream.Length) return false;
                    data = result;
                    return true;

                    Point3 Vector() => new Point3(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()), Finite(reader.ReadSingle()));
                    void Section(int bit, Func<object> read)
                    {
                        if ((flags & bit) == 0) return;
                        int first = reader.ReadByte();
                        int count = (first & 128) == 0 ? first : (first & 127) << 8 | reader.ReadByte();
                        if (count == 0 || (first & 128) != 0 && count < 128 ||
                            count * 4L > stream.Length - stream.Position) throw new InvalidDataException();
                        for (int i = 0; i < count; ++i)
                        {
                            int key = reader.ReadInt32(); object value = read();
                            var typedKey = new KeyValuePair<int, Type>(key, value.GetType());
                            if (result.values.ContainsKey(typedKey)) throw new InvalidDataException();
                            result.values.Add(typedKey, value);
                        }
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is FormatException || error is ArgumentException)
            { return false; }
        }

        // Inert source-backed rewrite; export policy must separately remove runtime authority and stale references.
        public string WriteNative(IEnumerable<KeyValuePair<int, object>> updates = null,
            IEnumerable<KeyValuePair<int, Type>> omit = null)
        {
            if (!NativeFlags.HasValue || !NativePrefabHash.HasValue)
                throw new InvalidOperationException("Not a native state packet.");
            var changed = new Dictionary<KeyValuePair<int, Type>, object>(values);
            int operations = 0;
            long updateBytes = 0;
            if (omit != null) foreach (var key in omit)
            {
                if (++operations > BlueprintForeignPartData.MaximumContainerBytes / 4)
                    throw new ArgumentException("Too many native state updates.");
                changed.Remove(key);
            }
            if (updates != null) foreach (var update in updates)
            {
                if (++operations > BlueprintForeignPartData.MaximumContainerBytes / 4 || update.Value == null)
                    throw new ArgumentException("Invalid native state update.");
                Type type = update.Value.GetType();
                if (type != typeof(float) && type != typeof(Point3) && type != typeof(Rotation3) && type != typeof(int) &&
                    type != typeof(long) && type != typeof(string) && type != typeof(byte[]))
                    throw new ArgumentException("Unsupported native state type.");
                updateBytes += update.Value is byte[] array ? array.LongLength :
                    update.Value is string text ? new UTF8Encoding(false, true).GetByteCount(text) :
                    type == typeof(Point3) ? 12 : type == typeof(Rotation3) ? 16 : type == typeof(long) ? 8 : 4;
                // Bound input before cloning; many individually valid arrays must not allocate an unbounded pending map.
                if (updateBytes > BlueprintForeignPartData.MaximumContainerBytes)
                    throw new ArgumentException("Native state updates are too large.");
                changed[new KeyValuePair<int, Type>(update.Key, type)] = update.Value is byte[] bytes ? bytes.Clone() : update.Value;
            }
            Type[] types = { typeof(float), typeof(Point3), typeof(Rotation3), typeof(int), typeof(long), typeof(string), typeof(byte[]) };
            var sections = types.Select(type => changed.Where(pair => pair.Key.Value == type).OrderBy(pair => pair.Key.Key).ToArray()).ToArray();
            ushort flags = (ushort)(NativeFlags.Value & 0x1f01);
            for (int i = 0; i < sections.Length; ++i)
            {
                if (sections[i].Length > 32767) throw new ArgumentException("Native state section is too large.");
                if (sections[i].Length != 0) flags |= (ushort)(2 << i);
            }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
            {
                writer.Write(flags); writer.Write(NativePrefabHash.Value);
                if (NativeRotationEuler.HasValue) Vector(NativeRotationEuler.Value);
                if (ConnectionType.HasValue)
                {
                    writer.Write(ConnectionType.Value); writer.Write(NativeConnectionUserId.Value); writer.Write(NativeConnectionId.Value);
                }
                foreach (var section in sections)
                {
                    if (section.Length == 0) continue;
                    if (section.Length < 128) writer.Write((byte)section.Length);
                    else { writer.Write((byte)(128 | (section.Length >> 8))); writer.Write((byte)section.Length); }
                    foreach (var pair in section)
                    {
                        writer.Write(pair.Key.Key);
                        switch (pair.Value)
                        {
                            case float f: writer.Write(Finite(f)); break;
                            case Point3 p: Vector(p); break;
                            case Rotation3 q:
                                writer.Write(Finite((float)q.X)); writer.Write(Finite((float)q.Y));
                                writer.Write(Finite((float)q.Z)); writer.Write(Finite((float)q.W)); break;
                            case int n: writer.Write(n); break;
                            case long n: writer.Write(n); break;
                            case string text:
                                int length = new UTF8Encoding(false, true).GetByteCount(text);
                                int prefix = 1; for (int n = length; n >= 128; n >>= 7) ++prefix;
                                RequireRoom((long)length + prefix); writer.Write(text); break;
                            case byte[] bytes: RequireRoom((long)bytes.Length + 4); writer.Write(bytes.Length); writer.Write(bytes); break;
                        }
                        RequireRoom(0);
                    }
                }
                string result = Convert.ToBase64String(stream.ToArray());
                if (!TryReadNative(result, NativePrefabHash.Value, out _)) throw new ArgumentException("Invalid native state output.");
                return result;

                void Vector(Point3 p) { writer.Write(Finite((float)p.X)); writer.Write(Finite((float)p.Y)); writer.Write(Finite((float)p.Z)); }
                void RequireRoom(long count)
                {
                    if (count > BlueprintForeignPartData.MaximumContainerBytes - stream.Length)
                        throw new ArgumentException("Native state packet is too large.");
                }
            }
        }

        // KG 1.7.1 has seven presence/count sections, not DataEntry's bitmask.
        // Accept canonical writer output only; no native DeserializeZDO or shared game maps are touched.
        public static bool TryReadKg(string base64, out BlueprintForeignZdo data)
        {
            data = null;
            if (base64 == null || base64.Length > BlueprintForeignPartData.MaximumContainerBase64) return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                if (bytes.Length < 7 || bytes.Length > BlueprintForeignPartData.MaximumContainerBytes ||
                    Convert.ToBase64String(bytes) != base64) return false;
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    var result = new BlueprintForeignZdo { kgDialect=true };
                    Section(() => (object)Finite(reader.ReadSingle()));
                    Section(() => new Point3(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()), Finite(reader.ReadSingle())));
                    Section(() => new Rotation3(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()), Finite(reader.ReadSingle()), Finite(reader.ReadSingle())));
                    Section(() => reader.ReadInt32()); Section(() => reader.ReadInt64());
                    Section(() => BlueprintContainerInventory.ReadString(reader, (int)(stream.Length - stream.Position)));
                    Section(() =>
                    {
                        int length = reader.ReadInt32();
                        if (length < 0 || length > stream.Length - stream.Position) throw new InvalidDataException();
                        return reader.ReadBytes(length);
                    });
                    if (stream.Position != stream.Length) return false;
                    data = result;
                    return true;

                    void Section(Func<object> read)
                    {
                        byte present = reader.ReadByte();
                        if (present > 1) throw new InvalidDataException();
                        if (present == 0) return;
                        int first = reader.ReadByte();
                        int count = (first & 128) == 0 ? first : (first & 127) << 8 | reader.ReadByte();
                        if (count == 0 || (first & 128) != 0 && count < 128 ||
                            count * 4L > stream.Length - stream.Position) throw new InvalidDataException();
                        for (int i = 0; i < count; ++i)
                        {
                            int key = reader.ReadInt32(); object value = read();
                            var typedKey = new KeyValuePair<int, Type>(key, value.GetType());
                            if (result.values.ContainsKey(typedKey)) throw new InvalidDataException();
                            result.values.Add(typedKey, value);
                        }
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is FormatException || error is ArgumentException)
            { return false; }
        }

        // Rewrite a KG packet without exposing or mutating its stored byte arrays or legacy dialect flags.
        public string WriteKg(IEnumerable<KeyValuePair<int,object>> updates=null,
            IEnumerable<KeyValuePair<int,Type>> omit=null)
        {
            if(!kgDialect) throw new InvalidOperationException("Not a KG state packet.");
            var changed=new Dictionary<KeyValuePair<int,Type>,object>(values);
            if(omit!=null) foreach(var key in omit) changed.Remove(key);
            int count=0;
            if(updates!=null) foreach(var update in updates)
            {
                if(++count>BlueprintForeignPartData.MaximumContainerBytes/4 || update.Value==null)
                    throw new ArgumentException("Invalid KG state update.");
                Type type=update.Value.GetType();
                if(type!=typeof(float) && type!=typeof(Point3) && type!=typeof(Rotation3) && type!=typeof(int) &&
                    type!=typeof(long) && type!=typeof(string) && type!=typeof(byte[])) throw new ArgumentException("Unsupported KG state type.");
                changed[new KeyValuePair<int,Type>(update.Key,type)]=update.Value is byte[] bytes?(byte[])bytes.Clone():update.Value;
            }
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
            {
                foreach(Type type in new[]{typeof(float),typeof(Point3),typeof(Rotation3),typeof(int),typeof(long),typeof(string),typeof(byte[])})
                {
                    var section=changed.Where(pair=>pair.Key.Value==type).OrderBy(pair=>pair.Key.Key).ToArray();
                    if(section.Length>32767) throw new ArgumentException("KG state section is too large.");
                    writer.Write(section.Length!=0);
                    if(section.Length==0) continue;
                    if(section.Length<128) writer.Write((byte)section.Length);
                    else {writer.Write((byte)(128|(section.Length>>8)));writer.Write((byte)section.Length);}
                    foreach(var pair in section)
                    {
                        writer.Write(pair.Key.Key);
                        switch(pair.Value)
                        {
                            case float f: writer.Write(f);break;
                            case Point3 p: writer.Write((float)p.X);writer.Write((float)p.Y);writer.Write((float)p.Z);break;
                            case Rotation3 q: writer.Write((float)q.X);writer.Write((float)q.Y);writer.Write((float)q.Z);writer.Write((float)q.W);break;
                            case int n: writer.Write(n);break;
                            case long n: writer.Write(n);break;
                            case string text:
                                if(Encoding.UTF8.GetByteCount(text)>BlueprintForeignPartData.MaximumContainerBytes) throw new ArgumentException("KG string is too large.");
                                writer.Write(text);break;
                            case byte[] bytes:
                                if(bytes.Length>BlueprintForeignPartData.MaximumContainerBytes) throw new ArgumentException("KG bytes are too large.");
                                writer.Write(bytes.Length);writer.Write(bytes);break;
                        }
                        if(stream.Length>BlueprintForeignPartData.MaximumContainerBytes) throw new ArgumentException("KG state packet is too large.");
                    }
                }
                string result=Convert.ToBase64String(stream.ToArray());
                if(!TryReadKg(result,out _)) throw new ArgumentException("Invalid KG state output.");
                return result;
            }
        }

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

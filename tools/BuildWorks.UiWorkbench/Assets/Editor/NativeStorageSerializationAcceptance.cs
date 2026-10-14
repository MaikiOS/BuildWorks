using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

internal static class NativeStorageSerializationAcceptance
{
    internal static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/storage-world-readback");
        string managed = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed";
        string path = Path.Combine(managed, "assembly_valheim.dll");
        using (var file = File.OpenRead(path))
        using (var hash = SHA256.Create())
            if (BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "") != "25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE")
                throw new InvalidOperationException("Native storage serializer fingerprint changed.");
        ResolveEventHandler resolve = (_, value) =>
        {
            string candidate = Path.Combine(managed, new AssemblyName(value.Name).Name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolve;
        try
        {
            Assembly game = Assembly.LoadFrom(path);
            Type zdoType = game.GetType("ZDO", true), extra = game.GetType("ZDOExtraData", true), packageType = game.GetType("ZPackage", true);
            object zdo = Activator.CreateInstance(zdoType);
            zdoType.GetProperty("Persistent").SetValue(zdo, true);
            zdoType.GetField("m_prefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(zdo, 43);
            object id = zdoType.GetField("m_uid").GetValue(zdo);
            Type kind = game.GetType("ZDOExtraData+Type", true);
            foreach (string name in new[] { "Long", "String", "ByteArray" })
                extra.GetMethod("Reserve").Invoke(null, new[] { id, Enum.Parse(kind, name), (object)1 });
            void Add(int key, object value) => extra.GetMethods().Single(method => method.Name == "Add" && method.GetParameters().Length == 3 &&
                method.GetParameters()[2].ParameterType == value.GetType()).Invoke(null, new[] { id, (object)key, value });
            Guid operation = new Guid("01234567-89ab-cdef-0123-456789abcdef");
            byte[] inventory = {109,0,0,0,0,0};
            Add(1, 100L); Add(2, operation.ToString("N")); Add(3, inventory);
            extra.GetMethod("PrepareSave").Invoke(null, null);
            object package = Activator.CreateInstance(packageType);
            void Write(object value) => packageType.GetMethod("Write", new[] {value.GetType()}).Invoke(package, new[] {value});
            Write((short)41); Write(1);
            zdoType.GetMethod("Save").Invoke(zdo, new[] {package});
            byte[] bytes = (byte[])packageType.GetMethod("GetArray").Invoke(package, null);
            byte[] payload = Bytes(w => {w.Write(41);w.Write("QA");w.Write("seed");w.Write(10);w.Write(71L);w.Write(1);w.Write(true);w.Write(0);w.Write(0);});
            byte[] meta = Bytes(w => {w.Write(payload.Length);w.Write(payload);});
            byte[] mapping = Bytes(w => {w.Write((short)41);w.Write(1);w.Write(1);w.Write((ushort)258);w.Write((byte)3);w.Write(1u);w.Write(1);});
            using (var metadata = new MemoryStream(meta, false))
            using (var map = new MemoryStream(mapping, false))
            using (var ok = new MemoryStream(BitConverter.GetBytes(41), false))
            {
                var expected = new NativeWorldStorageReadback.Expectation(71, operation, 43, 100, 1, 2, 3, inventory);
                if (!NativeWorldStorageReadback.TryVerify(metadata, map, ok, _ => new MemoryStream(bytes, false), expected, out var evidence, out string error))
                    throw new InvalidOperationException("Actual native ZDO.Save readback: " + error);
                if (evidence.Objects != 1 || evidence.Operation != operation) throw new InvalidOperationException("Native serializer receipt changed.");
            }
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "actual-native-storage.chunk"), bytes);
            File.WriteAllText(Path.Combine(output, "actual-native-serializer.txt"),
                "PASS actual installed ZDO constructor/ExtraData reserve/add/PrepareSave/ZDO.Save/ZPackage -> pure world41 reader; bytes=" + bytes.Length +
                "; no ZNet instance, world/player save, game objects, live inventory, settlement or network proof.");
            Debug.Log("BUILDWORKS_NATIVE_STORAGE_SERIALIZER_OK " + bytes.Length);
        }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolve; }
    }
    private static byte[] Bytes(Action<BinaryWriter> write)
    {
        using (var memory = new MemoryStream())
        { using (var writer = new BinaryWriter(memory, Encoding.UTF8, true)) write(writer); return memory.ToArray(); }
    }
}

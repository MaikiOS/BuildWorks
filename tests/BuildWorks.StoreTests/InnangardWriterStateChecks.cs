using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class InnangardWriterStateChecks
    {
        internal static void Run(string path, string expectedSha)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            Require(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "") == expectedSha,
                "Pinned QA assembly SHA differs.");
            var assembly = Assembly.LoadFrom(Path.GetFullPath(path));
            Require(assembly.GetName().Name == "BuildWorks", "Expected our QA assembly.");
            var writer = assembly.GetType("OstrixMods.BuildWorks.BuildWorksTestInnangardWriter", true);
            var pieceType = writer.GetNestedType("PieceState", BindingFlags.NonPublic);
            var serialize = writer.GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic);
            var sameState = writer.GetMethod("SameState", BindingFlags.Static | BindingFlags.NonPublic);
            Require(pieceType != null && serialize != null && sameState != null, "Actual QA snapshot contract is absent.");
            object New(string name) => Activator.CreateInstance(writer.GetNestedType(name, BindingFlags.NonPublic), true);
            string Json(object value) => (string)serialize.Invoke(null, new[] { value });
            var pieces = Array.CreateInstance(pieceType, 3);
            for (int i = 0; i < pieces.Length; ++i)
            {
                var piece = New("PieceState");
                Set(piece, "id", i + 10); Set(piece, "prefab", i == 2 ? "woodwall" : "wood_floor");
                Set(piece, "zdo", "8589934594:" + (i + 1));
                Set(piece, "position", new Vector3(i + .25f, i + .5f, i + .75f));
                Set(piece, "scale", new Vector3(1, 2, 3));
                Set(piece, "rotation", new Quaternion(0, .6f, 0, .8f)); pieces.SetValue(piece, i);
            }
            var state = New("RuntimeState"); Set(state, "pieces", pieces);
            var recipes = Enumerable.Range(0, 293).Select(i => "$recipe_" + i).ToArray(); recipes[292] = "юникод";
            var registry = Enumerable.Range(0, 257).Select(i => i + ":id_" + i + ":wood_floor").ToArray();
            Set(state, "inventory", "AQID"); Set(state, "recipes", recipes); Set(state, "registry", registry);
            Set(state, "worldObjects", 42); Set(state, "frame", 7); Set(state, "playerId", 8589934594L);
            Set(state, "yaw", 90f); Set(state, "playerPosition", new Vector3(1, 2, 3));
            Set(state, "playerRotation", new Quaternion(0, 0, 0, 1));
            string before = Json(state); var tree = Parse(before);
            object original;
            using (var bytes = new MemoryStream(Encoding.UTF8.GetBytes(before)))
                original = new DataContractJsonSerializer(state.GetType()).ReadObject(bytes);
            bool Equal() => (bool)sameState.Invoke(null, new[] { original, state });
            CheckPieces(tree, "pieces");
            Require(tree.Element("recipes").Elements().Select(e => e.Value).SequenceEqual(recipes), "Recipes lost.");
            Require(tree.Element("registry").Elements().Select(e => e.Value).SequenceEqual(registry), "Registry lost.");
            Require(tree.Element("playerId").Value == "8589934594", "64-bit identity changed.");
            Require(Json(state) == before && Equal(), "Unchanged snapshot must compare equal.");
            int changes = 0;
            void Changed(object target, string field, object replacement)
            {
                object old = Get(target, field); Set(target, field, replacement);
                try { Require(Json(state) != before && !Equal(), "Snapshot ignored changed " + field); ++changes; }
                finally { Set(target, field, old); }
                Require(Json(state) == before && Equal(), "Restored snapshot differs.");
            }
            Changed(state, "inventory", "changed"); Changed(state, "recipes", new[] { "changed" });
            Changed(state, "registry", new[] { "changed" }); Changed(state, "worldObjects", 99);
            var changedRecipes = recipes.ToArray(); changedRecipes[292] = "changed";
            var changedRegistry = registry.ToArray(); changedRegistry[256] = "changed";
            Changed(state, "recipes", changedRecipes); Changed(state, "registry", changedRegistry);
            Changed(state, "registry", registry.Reverse().ToArray());
            Changed(state, "frame", 99); Changed(state, "playerId", 99L); Changed(state, "yaw", 99f);
            Changed(state, "playerPosition", new Vector3(9, 8, 7));
            Changed(state, "playerRotation", new Quaternion(0, 0, 1, 0));
            var first = pieces.GetValue(0);
            Changed(first, "id", 99); Changed(first, "prefab", "changed"); Changed(first, "zdo", "9:9");
            Changed(first, "position", new Vector3(9, 8, 7)); Changed(first, "scale", new Vector3(9, 8, 7));
            Changed(first, "rotation", new Quaternion(0, 0, 1, 0));
            Changed(first, "position", new Vector3(.25000003f, .5f, .75f));
            Changed(pieces.GetValue(2), "zdo", "9:9");
            var fewer = Array.CreateInstance(pieceType, 2); Array.Copy(pieces, fewer, 2);
            Changed(state, "pieces", fewer);
            var rotation = writer.GetMethod("RawRotation", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { new Quaternion(.1f, .2f, .3f, .4f) });
            Require((float)Get(rotation, "x") == .1f && (float)Get(rotation, "w") == .4f,
                "Native quaternion was normalized before comparison.");
            var result = New("Result"); Set(result, "sourceParts", pieces);
            Set(result, "stateBefore", original); Set(result, "stateAfter", state);
            var output = Parse(Json(result)); CheckPieces(output, "sourceParts");
            CheckPieces(output.Element("stateBefore"), "pieces");
            CheckPieces(output.Element("stateAfter"), "pieces");
            foreach (string snapshot in new[] { "stateBefore", "stateAfter" })
            {
                Require(output.Element(snapshot).Element("recipes").Elements().Select(e => e.Value).SequenceEqual(recipes), "Report recipes lost.");
                Require(output.Element(snapshot).Element("registry").Elements().Select(e => e.Value).SequenceEqual(registry), "Report registry lost.");
            }
            bool rejected = false;
            try { CheckPieces(Parse("{\"inventoryRecipesRegistryCountFrameUnchanged\":true}"), "sourceParts"); }
            catch (InvalidOperationException ex) when (ex.Message == "Missing or incomplete sourceParts array.") { rejected = true; }
            Require(rejected, "Legacy missing-array result was accepted.");
            Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Innangard"), "Foreign assembly loaded.");
            Console.WriteLine("PASS actual QA DTO serializer:3 source rows/293 recipes/257 registry rows/both snapshots;" + changes + " field mutations rejected including beyond50;missing-array negative control rejected. No Unity host or author code executed.");
        }

        private static void CheckPieces(XElement root, string name)
        {
            var rows = root.Element(name)?.Elements().ToArray();
            Require(rows != null && rows.Length == 3, "Missing or incomplete " + name + " array.");
            for (int i = 0; i < rows.Length; ++i)
            {
                Require(rows[i].Element("id").Value == (i + 10).ToString() &&
                    rows[i].Element("zdo").Value == "8589934594:" + (i + 1) &&
                    rows[i].Element("prefab").Value == (i == 2 ? "woodwall" : "wood_floor"), "Piece binding changed.");
                foreach (string field in new[] { "position", "scale", "rotation" })
                {
                    Require(rows[i].Element(field)?.Elements().Count() == (field == "rotation" ? 4 : 3), "Transform fields lost.");
                    float[] expected = field == "position" ? new[] { i + .25f, i + .5f, i + .75f } :
                        field == "scale" ? new[] { 1f, 2f, 3f } : new[] { 0f, .6f, 0f, .8f };
                    string[] names = field == "rotation" ? new[] { "x", "y", "z", "w" } : new[] { "x", "y", "z" };
                    for (int j = 0; j < names.Length; ++j)
                        Require(float.Parse(rows[i].Element(field).Element(names[j]).Value, CultureInfo.InvariantCulture) == expected[j], "Transform value changed.");
                }
            }
        }
        private static XElement Parse(string json)
        { using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max)) return XElement.Load(reader); }
        private static object Get(object value, string name) => value.GetType().GetField(name).GetValue(value);
        private static void Set(object value, string name, object field)
        {
            var target = value.GetType().GetField(name);
            if (field is Vector3 || field is Quaternion)
            {
                object data = Activator.CreateInstance(target.FieldType);
                if (field is Vector3 vector)
                { Set(data, "x", vector.x); Set(data, "y", vector.y); Set(data, "z", vector.z); }
                else
                { var q = (Quaternion)field; Set(data, "x", q.x); Set(data, "y", q.y); Set(data, "z", q.z); Set(data, "w", q.w); }
                field = data;
            }
            target.SetValue(value, field);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}

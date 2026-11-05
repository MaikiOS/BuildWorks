using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ContactProfileChecks
    {
        internal static void Run(string pluginPath, string managedDirectory)
        {
            ResolveEventHandler resolve = (sender, args) => {
                string path = Path.Combine(managedDirectory, new AssemblyName(args.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            AppDomain.CurrentDomain.AssemblyResolve += resolve;
            try
            {
                var plugin = Assembly.LoadFrom(pluginPath);
                var type = plugin.GetType("OstrixMods.BuildWorks.BuildWorksTestContactProfiles", true);
                var snapshot = type.GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic);
                int called = 0;
                Action guard = () => { ++called; throw new InvalidOperationException("fixture-idle-refused"); };
                foreach (var page in new[] { new[] {-1,1}, new[] {0,0}, new[] {0,33} })
                {
                    bool rejected = false;
                    Exception observed = null;
                    try { snapshot.Invoke(null, new object[] { page[0],page[1],guard }); }
                    catch (TargetInvocationException error) { observed = error.InnerException; rejected = observed is ArgumentOutOfRangeException; }
                    Check(rejected && called == 0, "Invalid page reached idle/native code: " + observed);
                }
                bool noGuard = false;
                try { snapshot.Invoke(null, new object[] {0,1,null}); }
                catch (TargetInvocationException error) { noGuard = error.InnerException is ArgumentNullException; }
                Check(noGuard, "Missing idle guard accepted");
                bool idleRefused = false;
                try { snapshot.Invoke(null, new object[] {0,1,guard}); }
                catch (TargetInvocationException error) { idleRefused = error.InnerException.Message == "fixture-idle-refused"; }
                Check(idleRefused && called == 1, "Native registry reached before idle guard");
                var currentGhost = type.GetMethod("CurrentGhost", BindingFlags.Static | BindingFlags.NonPublic);
                noGuard = false; called = 0;
                try { currentGhost.Invoke(null, new object[] { null }); }
                catch (TargetInvocationException error) { noGuard = error.InnerException is ArgumentNullException; }
                Check(noGuard, "Ghost observation accepted a missing guard");
                idleRefused = false;
                try { currentGhost.Invoke(null, new object[] { guard }); }
                catch (TargetInvocationException error) { idleRefused = error.InnerException.Message == "fixture-idle-refused"; }
                Check(idleRefused && called == 1, "Ghost observation reached native access before its guard");
                var rowType = type.GetNestedType("Profile", BindingFlags.NonPublic);
                var pageType = type.GetNestedType("Page", BindingFlags.NonPublic);
                var first = Activator.CreateInstance(rowType, true); var second = Activator.CreateInstance(rowType, true);
                rowType.GetField("name").SetValue(first,"side-chest");
                rowType.GetField("supports").SetValue(first,false); rowType.GetField("noSupportWear").SetValue(first,true);
                rowType.GetField("colliders").SetValue(first,new[] { "BoxCollider" });
                var rows = Array.CreateInstance(rowType,2); rows.SetValue(first,0); rows.SetValue(second,1);
                var result = Activator.CreateInstance(pageType,true); pageType.GetField("profiles").SetValue(result,rows);
                var colliderType = type.GetNestedType("ColliderProfile", BindingFlags.NonPublic);
                var mesh = Activator.CreateInstance(colliderType,true);
                colliderType.GetField("type").SetValue(mesh,"UnityEngine.MeshCollider");
                colliderType.GetField("convex").SetValue(mesh,false);
                colliderType.GetField("readable").SetValue(mesh,false);
                colliderType.GetField("vertexCount").SetValue(mesh,24);
                var plain = Activator.CreateInstance(colliderType,true);
                colliderType.GetField("type").SetValue(plain,"UnityEngine.BoxCollider");
                var details = Array.CreateInstance(colliderType,2); details.SetValue(mesh,0); details.SetValue(plain,1);
                rowType.GetField("colliderDetails").SetValue(first,details);
                pageType.GetField("groundCollider").SetValue(result,mesh);
                using (var stream = new MemoryStream())
                {
                    new DataContractJsonSerializer(pageType).WriteObject(stream,result);
                    string json = Encoding.UTF8.GetString(stream.ToArray());
                    Check(json.Contains("\"supports\":false") && json.Contains("\"noSupportWear\":true") &&
                        Regex.Matches(json,"\"supports\":null").Count == 1 && Regex.Matches(json,"\"noSupportWear\":null").Count == 1 &&
                        json.Contains("\"colliders\":[\"BoxCollider\"]") && json.Contains("\"name\":\"side-chest\""),
                        "Actual QA DTO lost false/null/true values or typed arrays");
                    Check(json.Contains("\"colliderDetails\":[") && json.Contains("\"groundCollider\":{") &&
                        json.Contains("\"convex\":false") && json.Contains("\"convex\":null") &&
                        json.Contains("\"readable\":false") && json.Contains("\"readable\":null") &&
                        json.Contains("\"vertexCount\":24") && json.Contains("\"vertexCount\":null"),
                        "Collider metadata lost false/unknown distinction, ground or typed details");
                }
                var ghostType = type.GetNestedType("GhostObservation", BindingFlags.NonPublic);
                var ghost = Activator.CreateInstance(ghostType, true);
                ghostType.GetField("nativeId").SetValue(ghost,42); ghostType.GetField("sessionId").SetValue(ghost,43);
                ghostType.GetField("state").SetValue(ghost,"Editing"); ghostType.GetField("mode").SetValue(ghost,"WorldPrecision");
                ghostType.GetField("colliders").SetValue(ghost,details);
                using (var stream = new MemoryStream())
                {
                    new DataContractJsonSerializer(ghostType).WriteObject(stream,ghost);
                    string json = Encoding.UTF8.GetString(stream.ToArray());
                    Check(json.Contains("\"nativeId\":42") && json.Contains("\"sessionId\":43") &&
                        json.Contains("\"sameGhost\":false") && json.Contains("\"state\":\"Editing\"") &&
                        json.Contains("\"mode\":\"WorldPrecision\"") && json.Contains("\"colliders\":[") &&
                        json.Contains("\"convex\":null"), "Ghost DTO lost identity/state/false or typed collider data");
                }
                Console.WriteLine("PASS compiled QA prefab/ghost readers: early guards before native access; typed identity/state/false/null/true arrays preserved. No native registry/Unity/game invocation.");
            }
            finally { AppDomain.CurrentDomain.AssemblyResolve -= resolve; }
        }
        private static void Check(bool ok,string reason) { if(!ok)throw new InvalidOperationException(reason); }
    }
}

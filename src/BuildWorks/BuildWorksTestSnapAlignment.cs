#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksTestSnapAlignment
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        // Exercises the actual F9 join on transient, non-networked targets, not the live session.
        public static string Check()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            var before = new ZPackage();
            player.GetInventory().Save(before);
            int cases = 0;
            GameObject target = null;
            try
            {
                var type = typeof(PrecisionPlacementSession);
                var session = (PrecisionPlacementSession)FormatterServices.GetUninitializedObject(type);
                Action<string, object> set = (name, value) => type.GetField(name, Fields).SetValue(session, value);
                Func<string, object> get = name => type.GetField(name, Fields).GetValue(session);
                var join = type.GetMethod("TryAutoAlignToPiece", Fields);
                set("nativeSnapPoints", new List<Transform>());
                int start = (int)type.GetField("NativeAnchorStart", Fields | BindingFlags.Static).GetRawConstantValue();
                var anchors = Enumerable.Repeat(new Vector3(10000, 10000, 10000), start + 1).ToArray();
                Vector3 source = new Vector3(.7f, .1f, -.2f);
                anchors[start] = source;
                set("anchorLocalPoints", anchors);
                target = new GameObject("BuildWorks_QA_TransientSnapTarget");
                Piece piece = target.AddComponent<Piece>();
                var socket = new GameObject("snap");
                socket.tag = "snappoint";
                socket.transform.SetParent(target.transform, false);
                Vector3 point = new Vector3(10, 20, 30);
                foreach (Vector3 root in new[] { Vector3.zero, new Vector3(0, 180, 0),
                    new Vector3(90, 0, 0), new Vector3(0, 0, 90), new Vector3(17, 43, -21) })
                foreach (float yaw in new[] { 0f, 37f, 90f, 180f })
                {
                    target.transform.SetPositionAndRotation(point, Quaternion.Euler(root));
                    target.transform.localScale = new Vector3(1.3f, .8f, 1.1f);
                    socket.transform.position = point;
                    Quaternion rotation = Quaternion.Euler(11, yaw, -7);
                    Vector3 expected = point - rotation * source;
                    set("currentPosition", expected + new Vector3(.1f, .02f, .04f));
                    set("currentRotation", rotation);
                    for (int repeat = 0; repeat < 8; ++repeat)
                    {
                        join.Invoke(session, new object[] { piece });
                        Require(((Quaternion)get("currentRotation")).Equals(rotation), "Snap changed the chosen rotation.");
                        Require(Vector3.Distance((Vector3)get("currentPosition"), expected) < .0001f, "Snap missed the fixed socket.");
                        ++cases;
                    }
                }
                socket.tag = "Untagged";
                target.AddComponent<BoxCollider>();
                target.transform.localScale = Vector3.one;
                foreach (Vector3 root in new[] { Vector3.zero, new Vector3(0, 180, 0), new Vector3(17, 43, -21) })
                {
                    target.transform.SetPositionAndRotation(point, Quaternion.Euler(root));
                    Physics.SyncTransforms();
                    Quaternion rotation = Quaternion.Euler(11, 37, -7);
                    Vector3 near = target.transform.TransformPoint(new Vector3(0, 0, .55f));
                    Vector3 expected = near - rotation * source;
                    set("currentPosition", expected);
                    set("currentRotation", rotation);
                    Require(!(bool)join.Invoke(session, new object[] { piece }), "A surface without sockets was joined.");
                    Require(((Quaternion)get("currentRotation")).Equals(rotation), "A surface changed rotation.");
                    Require((Vector3)get("currentPosition") == expected, "A surface moved the piece without a socket.");
                    ++cases;
                }
                set("currentPosition", new Vector3(-100, -100, -100));
                Vector3 away = (Vector3)get("currentPosition");
                Require(!(bool)join.Invoke(session, new object[] { piece }) && (Vector3)get("currentPosition") == away,
                    "A distant target changed the transform.");
                ++cases;
                var after = new ZPackage();
                player.GetInventory().Save(after);
                Require(before.GetArray().SequenceEqual(after.GetArray()), "Alignment changed inventory.");
                return "passed=" + cases + "; rotation-preserved; socket-residual<0.0001; no-socket-unchanged; inventory-identical; transient-only";
            }
            finally { if (target) UnityEngine.Object.DestroyImmediate(target); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif

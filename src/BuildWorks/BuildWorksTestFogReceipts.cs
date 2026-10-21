#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Observes the existing project pool. Rendering stays with the shared ValheimQA CadenceProbe.
    internal static class BuildWorksTestFogReceipts
    {
#if BUILDWORKS_FOG_ORDINARY_BUDGET
        private const int ExpectedResidents = 4;
        private const long ExpectedBudget = 98304000, ExpectedReservation = 97423684;
#else
        private const int ExpectedResidents = 32;
        private const long ExpectedBudget = 256L * 1024 * 1024, ExpectedReservation = 230134084;
#endif
        [Serializable] private sealed class Frame
        {
            public double realtime;
            public int renders, controlledRenders;
            public float clock, sunIntensity;
            public Vector3 sunDirection;
            public long fieldBytes, arenaRenderBytes, velocityRamBytes, graphicsDriverBytes;
            public BlueprintFogChannels.ReceiptInput[] inputs;
            public FogPerformanceCounters.Snapshot counters;
        }
        [Serializable] private sealed class Report
        {
            public string state = "preparing", error = "", mode, project, path;
            public int arm;
            public int windowSeconds = 5;
            public long world;
            public long fieldBudget, reservedFieldBytes;
            public int residentCapacity;
            public bool velocityUpload, forcedReceiptMode;
            public bool inventoryUnchanged, completedWindow, written;
            public Frame baseline, final;
            public List<Frame> samples = new List<Frame>(212);
        }
        private static BlueprintConstructionProjectRuntime runtime;
        private static BlueprintConstructionProjectFog fog;
        private static Player player;
        private static Vector3 playerPosition;
        private static Guid project;
        private static OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData inventory;
        private static Report report;
        private static double preparingAt, sampleAt;
        private static int arms;
        private static PropertyInfo cadenceOwned;
        private static MethodInfo cadenceStatus;
        internal static bool OwnsFog => fog != null;
        [Serializable] private sealed class CadenceStatus
        { public string state = ""; public bool rendersCamera = false; public int renderedFrames = 0; }

        public static string Prepare(string mode, string id) => Begin(mode, id, 5);
        public static string PrepareLong(string mode, string id) => Begin(mode, id, 52);

        private static string Begin(string mode, string id, int windowSeconds)
        {
            if (mode != "individual" && mode != "grouped" && mode != "default") throw new ArgumentException("Choose individual, grouped or default receipts.");
            if (report != null && (report.state != "complete" && report.state != "failed" || !report.written))
                throw new InvalidOperationException("Previous receipt arm is live or its report was not saved.");
            if (arms >= 4) throw new InvalidOperationException("Close the completed four-arm comparison first.");
            var owner = BuildWorksTestIsolation.RequireLoadedSession(); BuildWorksPlugin.RequireQaStorageIdle();
            var target = Guid.ParseExact(id, "N");
            var current = BuildWorksPlugin.ConstructionProjects;
            var cadence = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Ostrix.ValheimQa.CadenceProbe", false))
                .SingleOrDefault(type => type != null) ?? throw new InvalidOperationException("Load the existing ValheimQA render-probe first.");
            cadenceOwned = cadence.GetProperty("CameraOwned", BindingFlags.Static | BindingFlags.NonPublic);
            cadenceStatus = cadence.GetMethod("Status", BindingFlags.Static | BindingFlags.Public);
            if (cadenceOwned == null || cadenceStatus == null || (bool)cadenceOwned.GetValue(null))
                throw new InvalidOperationException("Prepare before starting ValheimQA CadenceProbe.StartRendered.");
            if (!BlueprintStorageCapture.TrySnapshotInventory(owner.GetInventory(), out var before))
                throw new InvalidOperationException("Receipt comparison inventory is unavailable.");
            string directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "ValAgentBridge", "test-results");
            Directory.CreateDirectory(directory);
            // Reset presentation only; do not reload journals, boards, cleanup worksets or durable project data.
            current.QaResetFogForReceipts(target, mode == "default" ? (bool?)null : mode == "individual");
            runtime = current; fog = runtime.Fog; player = owner; playerPosition = owner.transform.position;
            project = target; inventory = before;
            report = new Report { mode = mode, arm = ++arms, project = id, world = runtime.World, windowSeconds = windowSeconds,
                fieldBudget = BlueprintFogChannels.MaximumFieldBudget, forcedReceiptMode = current.QaImmediateFogReceipts.HasValue,
                path = Path.Combine(directory, "fog-receipt-arm-" + Guid.NewGuid().ToString("N") + ".json") };
            preparingAt = Time.realtimeSinceStartupAsDouble;
            return Status();
        }

        public static string Arm()
        {
            if (report?.state != "ready") throw new InvalidOperationException("Receipt preparation must be ready before arming.");
            Context();
            if (!(bool)cadenceOwned.GetValue(null)) throw new InvalidOperationException("Start ValheimQA CadenceProbe.StartRendered before arming.");
            double remaining = 90 - (Time.realtimeSinceStartupAsDouble - preparingAt);
            if (remaining < report.windowSeconds + 2)
                throw new InvalidOperationException("Receipt window cannot fit the remaining preparation/window budget: " + remaining + "s.");
            report.state = "warming";
            return Status();
        }

        internal static bool Frozen(BlueprintConstructionProjectFog current)
        {
            if (!ReferenceEquals(current, fog) || report == null) return false;
            if (report.state == "complete" || report.state == "failed") return true;
            try { Context(); }
            catch (Exception error) { Finish(error.Message); return true; }
            return report.state == "ready" || report.state == "warming" && fog.QaChannels.Count == 28;
        }
        private static void Context()
        {
            if (!player || player != Player.m_localPlayer || player.IsDead() ||
                Vector3.Distance(player.transform.position, playerPosition) > .1f || runtime != BuildWorksPlugin.ConstructionProjects ||
                runtime.World != report.world || runtime.BlocksInput || !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement ||
                runtime.Fog != fog) throw new InvalidOperationException("Receipt comparison lost its stationary isolated project context.");
            if (fog.Error(project) != null) throw new InvalidOperationException(fog.Error(project));
            if ((report.state == "warming" || report.state == "recording") && !(bool)cadenceOwned.GetValue(null))
                throw new InvalidOperationException("Receipt comparison lost its controlled rendered camera.");
            if (fog.QaChannels?.ReceiptInputLimitReached() == true)
                throw new InvalidOperationException("Receipt arm stopped at its input/fault guard before another observation.");
            if (Time.realtimeSinceStartupAsDouble - preparingAt > 90) throw new InvalidOperationException("Receipt arm exceeded its bounded preparation/window lifetime.");
        }
        internal static void Observe(BlueprintConstructionProjectFog current)
        {
            if (!ReferenceEquals(current, fog) || report == null || report.state == "complete" || report.state == "failed") return;
            try
            {
                Context();
                var building = fog.Building(project);
                if (building?.Ready != true || fog.VisibleParts != 28) return;
                var channels = fog.QaChannels;
                var arena = channels.SnapshotArena;
                report.residentCapacity = arena.ResidentCapacity; report.reservedFieldBytes = arena.ReservedFieldBytes;
                report.velocityUpload = arena.VelocityUpload;
                if (channels.ReadReceiptInputs().Length != 28 || arena.ResidentCapacity != ExpectedResidents || arena.BlockCapacity != 32768 ||
                    report.fieldBudget != ExpectedBudget || arena.ReservedFieldBytes != ExpectedReservation ||
                    report.velocityUpload != (ExpectedResidents < 32) ||
                    channels.QaImmediateReceiptReads != (report.mode == "individual") ||
                    runtime.QaImmediateFogReceipts.HasValue != (report.mode != "default"))
                    throw new InvalidOperationException("Receipt arm pool/mode differs from the frozen comparison.");
                var now = Time.realtimeSinceStartupAsDouble;
                if (report.state == "preparing")
                { report.state = "ready"; return; }
                if (report.state == "ready") return;
                if (report.state == "warming")
                {
                    if (channels.Count != 28 || fog.RenderFrames < 1) return;
                    report.baseline = Read(now); report.state = "recording"; sampleAt = now;
                }
                if (channels.Count != 28) throw new InvalidOperationException("Receipt window lost a snapshot owner.");
                if (now - sampleAt >= .25 || now - report.baseline.realtime >= report.windowSeconds)
                {
                    var frame = Read(now); report.samples.Add(frame); sampleAt = now;
                    if (frame.inputs.Any(input => input.faulted)) throw new InvalidOperationException("Receipt source faulted.");
                    if (frame.inputs.Any(input => input.queued >= 48)) throw new InvalidOperationException("Receipt arm stopped before the 64-sample input limit.");
                    if (now - report.baseline.realtime >= report.windowSeconds)
                    {
                        report.final = frame;
                        int rendered = frame.renders - report.baseline.renders;
                        if (rendered < 1 || rendered != frame.controlledRenders - report.baseline.controlledRenders ||
                            frame.clock - report.baseline.clock < report.windowSeconds * .9f)
                            throw new InvalidOperationException("Receipt window did not retain controlled rendering and the production clock.");
                        report.completedWindow = true; Finish(null);
                    }
                }
            }
            catch (Exception error) { Finish(error.Message); }
        }
        private static Frame Read(double now)
        {
            var channels = fog.QaChannels; var counters = channels.ReadPerformanceProbe();
            if (counters == null || !counters.valid) throw new InvalidOperationException("Receipt performance evidence is invalid.");
            var cadence = JsonUtility.FromJson<CadenceStatus>((string)cadenceStatus.Invoke(null, null));
            if (cadence == null || cadence.state != "recording" || !cadence.rendersCamera)
                throw new InvalidOperationException("Controlled cadence is not recording rendered frames.");
            Light sun = RenderSettings.sun;
            return new Frame { realtime = now, renders = fog.RenderFrames, controlledRenders = cadence.renderedFrames, clock = fog.Building(project).Clock,
                inputs = channels.ReadReceiptInputs(), counters = counters, sunIntensity = sun ? sun.intensity : 0,
                fieldBytes = channels.FieldBytes, arenaRenderBytes = channels.SnapshotArena.RenderBytes,
                velocityRamBytes = channels.VelocityRamBytes, graphicsDriverBytes = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver(),
                sunDirection = sun ? sun.transform.forward : Vector3.down };
        }
        private static void Finish(string error)
        {
            if (report.state == "complete" || report.state == "failed") return;
            report.state = error == null ? "complete" : "failed"; report.error = error ?? "";
            try
            {
                if (report.final == null && report.baseline != null && fog?.QaChannels != null)
                    try { report.final = Read(Time.realtimeSinceStartupAsDouble); }
                    catch (Exception readError) { report.state = "failed"; report.error += " Final snapshot: " + readError.Message; }
                report.inventoryUnchanged = player && BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var after) &&
                    inventory.Info == after.Info && inventory.Zdo == after.Zdo;
                if (!report.inventoryUnchanged) { report.state = "failed"; report.error += " Inventory changed."; }
                report.written = true;
                string pending = report.path + ".pending";
                using (var output = File.Create(pending)) Serializer().WriteObject(output, report);
                using (var input = File.OpenRead(pending))
                {
                    var saved = (Report)Serializer().ReadObject(input);
                    if (saved.samples.Count != report.samples.Count || (saved.baseline == null) != (report.baseline == null) ||
                        (saved.final == null) != (report.final == null)) throw new InvalidOperationException("Receipt evidence did not survive serialization.");
                }
                File.Move(pending, report.path);
            }
            catch (Exception failure) { report.written = false; report.state = "failed"; report.error += " Report: " + failure.Message; }
        }
        private static DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(Report),
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 65536 });

        public static string CheckSerialization()
        {
            if (BlueprintFogChannels.MaximumFieldBudget != ExpectedBudget ||
                BlueprintFogSnapshotState.EstimateFieldBytes(32, 32768, ExpectedResidents, BlueprintFogLedgerLayout.Spatial3D) != ExpectedReservation)
                throw new InvalidOperationException("Receipt probe did not compile the expected field budget/allocation.");
            var counters = new FogPerformanceCounters(System.Diagnostics.Stopwatch.Frequency).Read();
            counters.physicalReceiptRequests = 7; counters.flows[0].accepted = 8;
            var frame = new Frame { realtime = 1.25, renders = 2, controlledRenders = 2, clock = 3,
                sunDirection = new Vector3(1, 2, 3), inputs = Enumerable.Range(0, 28).Select(index =>
                    new BlueprintFogChannels.ReceiptInput { queued = index + 4, confirmedTick = index + 5 }).ToArray(),
                counters = counters };
            var value = new Report { baseline = frame, final = frame, windowSeconds = 52 };
            for (int i = 0; i < 210; ++i) value.samples.Add(frame);
            using (var stream = new MemoryStream())
            {
                Serializer().WriteObject(stream, value); stream.Position = 0;
                var saved = (Report)Serializer().ReadObject(stream);
                if (saved.baseline?.realtime != 1.25 || saved.final?.sunDirection != frame.sunDirection || saved.samples.Count != 210 || saved.windowSeconds != 52 ||
                    saved.samples[0].inputs.Length != 28 || saved.samples[1].inputs[27].queued != 31 || saved.samples[1].inputs[27].confirmedTick != 32 ||
                    saved.final.counters.physicalReceiptRequests != 7 || saved.final.counters.flows[0].accepted != 8)
                    throw new InvalidOperationException("Nested receipt evidence did not survive serialization.");
            }
            return "nested-receipt-evidence-roundtrip-passed";
        }
        public static string Status()
        {
            if (report == null) return "{\"state\":\"absent\"}";
            return JsonUtility.ToJson(new Summary { state = report.state, error = report.error, path = report.path, mode = report.mode,
                arm = report.arm, completedWindow = report.completedWindow, inventoryUnchanged = report.inventoryUnchanged,
                written = report.written, renders = (report.final?.renders ?? fog.RenderFrames) - (report.baseline?.renders ?? 0),
                seconds = report.final != null && report.baseline != null ? report.final.realtime - report.baseline.realtime : 0,
                maximumQueue = report.final?.inputs?.Max(input => input.queued) ?? 0 });
        }
        public static string Close()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            if (report != null && report.state != "complete" && report.state != "failed") Finish("Receipt comparison explicitly stopped.");
            if (runtime != null) runtime.QaResetFogForReceipts(project, null);
            fog = null; runtime = null; player = null; report = null; inventory = null; arms = 0;
            return "receipt-comparison-closed;normal-presentation-restored";
        }
        [Serializable] private sealed class Summary
        {
            public string state, error, path, mode;
            public int arm, renders, maximumQueue;
            public double seconds;
            public bool completedWindow, inventoryUnchanged, written;
        }
    }
}
#endif

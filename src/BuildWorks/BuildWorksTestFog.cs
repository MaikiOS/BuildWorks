#if BUILDWORKS_TEST_STAND
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using HarmonyLib;

namespace OstrixMods.BuildWorks
{
    internal sealed class BuildWorksTestFog : MonoBehaviour
    {
        private static BuildWorksTestFog borrowedProbe;
        public static BuildWorksTestFog CurrentProbe => borrowedProbe ? borrowedProbe : Camera.main ? Camera.main.GetComponent<BuildWorksTestFog>() : null;
        public static string HouseStatus()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = CurrentProbe;
            return HouseJson(new HouseReceipt {
                active = fog && fog.Active && fog.PublishedHouse, preparing = fog && fog.preparation != null, failure = fog?.Failure,
                expectedParts = fog?.building?.PlannedParts ?? 0, preparedParts = fog?.building?.PreparedParts ?? 0,
                preparationPhase = fog?.building?.PreparationPhase, preparationSteps = fog?.preparationSteps ?? 0,
                preparationMilliseconds = fog?.PreparationMilliseconds ?? 0, maxPreparationStepMilliseconds = fog?.maxPreparationStepMilliseconds ?? 0,
                passPhase = fog?.PassPhase, passSide = fog?.PassSide ?? -1, passFailure = fog?.PassFailure,
                passRunning = fog && fog.PassRunning,
                firstFaultJson = fog?.FirstFaultJson,
                parts = fog?.PartCount ?? 0, owners = fog?.ActiveOwners ?? 0, residentOwners = fog?.ResidentOwners ?? 0,
                visibleParts = fog?.building?.VisibleCount ?? 0, assets = fog?.assets?.AssetCount ?? 0, kernels = fog?.assets?.KernelCount ?? 0,
                blockCapacity = fog?.channels?.SnapshotArena?.BlockCapacity ?? 0,
                residentCapacity = fog?.channels?.SnapshotArena?.ResidentCapacity ?? 0,
                ledgerCells = fog?.channels?.SnapshotArena?.LedgerCells ?? 0,
                queuedSamples = fog?.building?.QueuedSamples ?? 0, minimumTick = fog?.building?.MinimumConfirmedTick ?? 0,
                lastFlags = fog?.building?.LastFlags ?? 0, frames = fog?.borrowedProject?.Fog?.RenderFrames ?? fog?.RenderFrames ?? 0,
                fieldBytes = fog?.FieldBytes ?? 0, renderBytes = fog?.borrowedProject?.Fog?.RenderBytes ?? fog?.AtlasBytes ?? 0, velocityRamBytes = fog?.VelocityRamBytes ?? 0,
                inputs = fog?.channels?.ReadReceiptInputs() ?? Array.Empty<BlueprintFogChannels.ReceiptInput>(),
                origin = fog?.Origin ?? Vector3.zero, clock = fog?.building?.Clock ?? fog?.clock ?? 0,
                playerPosition = Player.m_localPlayer.transform.position,
                processMemory = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 });
        }
        private static string HouseJson(HouseReceipt value)
        {
            value.failure ??= ""; value.passFailure ??= "";
            value.preparationPhase ??= ""; value.passPhase ??= ""; value.firstFaultJson ??= "";
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(HouseReceipt)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static string CheckHouseSerialization()
        {
            var value = new HouseReceipt { frames = 43, clock = 1.25f, playerPosition = new Vector3(3, 4, 5),
                inputs = new BlueprintFogChannels.ReceiptInput[28] };
            for (int i = 0; i < value.inputs.Length; ++i)
                value.inputs[i] = new BlueprintFogChannels.ReceiptInput { queued = i, capturedTick = i + 42, confirmedTick = i + 40 };
            string json = HouseJson(value);
            if (!json.Contains("\"failure\":\"\"") || !json.Contains("\"passFailure\":\"\""))
                throw new InvalidOperationException("House receipt changed its successful string schema.");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var saved = (HouseReceipt)new DataContractJsonSerializer(typeof(HouseReceipt)).ReadObject(stream);
                if (saved.inputs?.Length != 28 || saved.inputs[27].queued != 27 || saved.inputs[27].capturedTick != 69 ||
                    saved.inputs[27].confirmedTick != 67 || saved.frames != 43 || saved.clock != 1.25f || saved.playerPosition != value.playerPosition)
                    throw new InvalidOperationException("House receipt lost nested source observations.");
            }
            return "house-receipt-28-source-roundtrip-passed";
        }
        [Serializable] private sealed class HouseReceipt
        {
            public bool active, preparing, passRunning;
            public string failure, preparationPhase;
            public string passPhase, passFailure;
            public int passSide;
            public int expectedParts, preparedParts, preparationSteps;
            public double preparationMilliseconds, maxPreparationStepMilliseconds;
            public int parts, owners, residentOwners, queuedSamples, minimumTick, frames, visibleParts, assets, kernels;
            public uint lastFlags;
            public string firstFaultJson;
            public int blockCapacity, residentCapacity;
            public int ledgerCells;
            public long fieldBytes, renderBytes, velocityRamBytes, processMemory;
            public Vector3 origin, playerPosition;
            public BlueprintFogChannels.ReceiptInput[] inputs;
            public float clock;
        }
        // GABS can call handlers but cannot retain an internal constructor result for this resource-only probe.
        public static string VerifyPublication()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var result = new PublicationReceipt();
            RenderTexture pages;
            using (var probeAssets = BlueprintFogAssets.LoadEmbedded())
            using (var pool = BlueprintFogChannels.CreateArena(probeAssets, 2, 8192))
            {
                var arena = pool.SnapshotArena; pages = arena.Bricks;
                if (!pages || !pages.IsCreated() || arena.Smoke || arena.Surface || arena.RenderBytes != 14478912)
                    throw new InvalidOperationException("Native fog publication allocated unexpected render resources.");
                var token = new BlueprintFogSnapshotState.Submission(1, 1, 1, 1, 1);
                arena.PublishMetadata(1, token, Vector3.zero, new Vector2(.3f, .95f), .1f, 1, mapBase: 86400);
                var bases = new uint[2]; arena.Buffers["_PublishedMapBases"].GetData(bases);
                if (bases[1] != 86400) throw new InvalidOperationException("Native GPU did not publish the expected map bank.");
                var metadata = new uint[32]; arena.Buffers["_PublishedMetadata"].GetData(metadata);
                var control = new uint[16]; control[2] = 8; arena.Buffers["_Control"].SetData(control);
                arena.PublishMetadata(1, token, Vector3.one, Vector2.one, .2f, 0, mapBase: 57600);
                arena.Buffers["_PublishedMapBases"].GetData(bases);
                if (bases[1] != 86400) throw new InvalidOperationException("Rejected native publication changed the confirmed map bank.");
                var retained = new uint[32]; arena.Buffers["_PublishedMetadata"].GetData(retained);
                for (int i = 0; i < metadata.Length; ++i)
                    if (metadata[i] != retained[i]) throw new InvalidOperationException("Rejected native publication changed the confirmed metadata.");
                result.renderBytes = arena.RenderBytes; result.reservedFieldBytes = arena.ReservedFieldBytes;
                result.assets = probeAssets.AssetCount; result.kernels = probeAssets.KernelCount;
            }
            if (pages) throw new InvalidOperationException("Native publication pages survived their pool teardown.");
            result.passed = result.packingRejectRetained = result.pagesReleased = true;
            return JsonUtility.ToJson(result);
        }
        [Serializable] private sealed class PublicationReceipt
        {
            public bool passed, packingRejectRetained, pagesReleased;
            public bool gameHostTested = true, visualParityTested = false, constructionTested = false, wholeHouseSupported = false;
            public long renderBytes, reservedFieldBytes;
            public int assets, kernels;
        }

        public static string VerifyVelocityPaging()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var shape = new Texture3D(4, 4, 4, TextureFormat.RGBAHalf, false);
            var result = new VelocityReceipt(); Texture3D upload;
            try
            {
                shape.SetPixelData(new ushort[256], 0); shape.Apply(false, false);
                using (var probeAssets = BlueprintFogAssets.LoadEmbedded())
                using (var pool = BlueprintFogChannels.CreateArena(probeAssets, 28, 8192))
                {
                    upload = pool.SnapshotArena.VelocityUpload;
                    var initial = new BlueprintFogInputQueue.Sample(0, new Geometry.Point3(0, 0, 0), .3f, .95f, false, false, false);
                    BlueprintFogChannels.SnapshotChannel owner = null;
                    for (int i = 0; i < 28; ++i)
                    {
                        var created = pool.AcquireSnapshot(i, initial, (scratch, staged) => new BlueprintFogSimulation(probeAssets,
                            shape, shape, shape, Vector3.one * -2, Vector3.one * 2, -Vector3.one, Vector3.one,
                            Vector3.zero, 37, scratch: scratch, stagedState: staged));
                        if (i == 0) owner = created;
                    }
                    if (pool.Count != 28 || pool.ResidentSnapshotCount != 7 || pool.FieldBytes != 94478116)
                        throw new InvalidOperationException("Native paged pool reservation differs.");
                    var pattern = new ushort[2457600];
                    for (int i = 0; i < pattern.Length; ++i) pattern[i] = (ushort)(i * 17 % 0x7c00);
                    upload.SetPixelData(pattern, 0); upload.Apply(false, false); Graphics.CopyTexture(upload, owner.Velocity);
                    pool.PageSnapshotVelocity(owner); owner.evictionRequest.WaitForCompletion(); pool.PollSnapshots();
                    if (owner.Velocity || owner.coldVelocity == null || !pool.EnsureSnapshotVelocity(owner))
                        throw new InvalidOperationException("Native velocity paging did not restore its owner.");
                    var read = AsyncGPUReadback.Request(owner.Velocity, 0); read.WaitForCompletion();
                    if (read.hasError) throw new InvalidOperationException("Native restored velocity readback failed.");
                    int offset = 0;
                    for (int layer = 0; layer < read.layerCount; ++layer)
                    {
                        var data = read.GetData<ushort>(layer);
                        for (int i = 0; i < data.Length; ++i)
                            if (offset >= pattern.Length || data[i] != pattern[offset++]) throw new InvalidOperationException("Native velocity half bits differ.");
                    }
                    if (offset != pattern.Length) throw new InvalidOperationException("Native velocity page was truncated.");
                    result.fieldBytes = pool.FieldBytes; result.rawWords = offset;
                }
                if (upload) throw new InvalidOperationException("Native velocity upload survived teardown.");
                result.passed = result.exactVelocity = result.uploadReleased = true;
                return JsonUtility.ToJson(result);
            }
            finally { DestroyImmediate(shape); }
        }
        [Serializable] private sealed class VelocityReceipt
        {
            public bool passed, exactVelocity, uploadReleased;
            public bool gameHostTested = true, visualParityTested = false, wholeHouseSupported = false;
            public int logicalOwners = 28, residentLimit = 7, rawWords;
            public long fieldBytes;
        }

        private BlueprintFogAssets assets;
        private BlueprintFogVisual visual;
        private BlueprintFogVisual second;
        private BlueprintFogBuilding building;
        private BlueprintConstructionProjectRuntime borrowedProject;
        private Guid borrowedProjectId;
        private Vector3 borrowedCentre;
        private Vector2 borrowedDistances;
        private BlueprintFogChannels channels;
        private BlueprintFogVolume volume;
        private Player player;
        private ZNet network;
        private CapsuleCollider capsule;
        private Camera view;
        private DepthTextureMode oldDepth, ownedDepth;
        private float clock;
        private System.Collections.Generic.IEnumerator<object> preparation;
        private float preparationStarted;
        private double preparationMilliseconds, maxPreparationStepMilliseconds;
        private int preparationSteps;
        private double PreparationMilliseconds => preparation != null ? (Time.realtimeSinceStartup - preparationStarted) * 1000d : preparationMilliseconds;
        public int RenderFrames { get; private set; }
        public int Tick => visual?.Simulation?.Tick ?? (building == null ? 0 : Mathf.FloorToInt(building.Clock / BlueprintFogSimulation.Step + .0001f));
        public Vector3 Origin => borrowedProject != null ? borrowedCentre : visual?.Origin ?? building?.Origin ?? Vector3.zero;
        public bool Active => Failure == null && (visual != null || building?.Ready == true);
        public int PartCount => building?.Count ?? (visual == null ? 0 : second == null ? 1 : 2);
        public long FieldBytes => channels?.FieldBytes ?? visual?.Simulation?.AllocatedFieldBytes ?? 0;
        public long AtlasBytes => building?.AtlasBytes ?? volume?.AtlasBytes ?? 0;
        public bool PublishedHouse => building != null && channels?.SnapshotArena != null;
        public long VelocityRamBytes => PublishedHouse ? channels.VelocityRamBytes : 0;
        public int ResidentOwners => PublishedHouse ? channels.ResidentSnapshotCount : 0;
        public long PeakFieldBytes { get; private set; }
        public int ActiveOwners => channels?.Count ?? (visual == null ? 0 : 1);
        public bool SkipVolume { get; set; }
        public bool SkipSimulation { get; set; }
        private string failure;
        public string Failure
        {
            get => failure ?? (borrowedProject == null ? null : !player || player != Player.m_localPlayer || !network || network != ZNet.instance ||
                !Game.instance || Game.instance.IsShuttingDown() ? "Borrowed QA session changed." : borrowedProject.FogError(borrowedProjectId) ??
                (borrowedProject.Fog?.Building(borrowedProjectId) == building ? null : "Borrowed construction fog changed."));
            private set => failure = value;
        }
        public Bounds PrefabBounds { get; private set; }
        public string PassPhase { get; private set; } = "idle";
        public int PassSide { get; private set; } = -1;
        public string PassFailure { get; private set; }
        public bool PassRunning => wallPass != null;
        private bool wallLine;
        private BlueprintFogChannels.MetricsProbe metrics;
        public static string AttachExisting(string id)
        {
            var owner = BuildWorksTestIsolation.RequireLoadedSession(); BuildWorksPlugin.RequireQaStorageIdle();
            var runtime = BuildWorksPlugin.QaConstructionProjects;
            if (CurrentProbe || BuildWorksTestFogReceipts.OwnsFog || !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement ||
                runtime == null || runtime.BlocksInput || runtime.OpenBoard)
                throw new InvalidOperationException("Close the stationary receipt observer and other QA probes before borrowing a private project.");
            var project = runtime.Read(Guid.ParseExact(id, "N")); var progress = runtime.Progress(project.Id);
            if (project.Cancelled || progress.Built != 0 || progress.NeedsReview || progress.Total < 1 || progress.Total > 32 ||
                runtime.Fog?.Building(project.Id)?.Ready != true || runtime.FogError(project.Id) != null)
                throw new InvalidOperationException("A ready, wholly unpaid preserved project is required.");
            var blueprint = project.Blueprint; bool first = true; Bounds bounds = default;
            foreach (var part in project.Parts)
            {
                var prefab = ZNetScene.instance.GetPrefab(blueprint.parts[part.SourceIndex].prefabName);
                if (!prefab || !BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(prefab, out var local, true))
                    throw new InvalidOperationException("Preserved project has no native visual bounds.");
                var pose = part.WorldPose.Values;
                var matrix = Matrix4x4.TRS(new Vector3(pose[0], pose[1], pose[2]), new Quaternion(pose[3], pose[4], pose[5], pose[6]),
                    Vector3.Scale(prefab.transform.localScale, new Vector3(pose[7], pose[8], pose[9])));
                for (int corner = 0; corner < 8; ++corner)
                {
                    var point = matrix.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y, (corner & 4) == 0 ? local.min.z : local.max.z));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            if (first || bounds.size.magnitude > 25 || Vector3.Distance(owner.transform.position, bounds.center) > 20)
                throw new InvalidOperationException("Preserved project route exceeds the bounded loaded QA site.");
            var probe = new GameObject("BuildWorks_QA_BorrowedProjectRoute").AddComponent<BuildWorksTestFog>();
            try { probe.AttachProject(runtime, project.Id, bounds.center, new Vector2(bounds.extents.x + 2, bounds.extents.z + 2)); }
            catch { UnityEngine.Object.DestroyImmediate(probe.gameObject); throw; }
            return HouseStatus();
        }
        public static string DetachExisting()
        {
            BuildWorksTestIsolation.RequireLoadedSession(); var probe = CurrentProbe;
            if (!probe || probe.borrowedProject == null || probe.PassRunning)
                throw new InvalidOperationException("An idle borrowed route is required; the construction project is never cancelled here.");
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            return "borrowed-route-detached;project-retained";
        }
        internal void AttachProject(BlueprintConstructionProjectRuntime runtime, Guid id, Vector3 centre, Vector2 distances)
        {
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (building != null || visual != null || runtime.Fog?.Building(id)?.Ready != true || runtime.FogError(id) != null)
                throw new InvalidOperationException("An idle probe and a ready real construction project are required.");
            borrowedProject = runtime; borrowedProjectId = id; borrowedCentre = centre;
            borrowedDistances = distances;
            building = runtime.Fog.Building(id); channels = runtime.Fog.QaChannels; wallLine = true;
            borrowedProbe = this;
        }
        public static string BeginPublishedMetrics(bool compareBricks = false)
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = CurrentProbe;
            if (!fog || !fog.Active || !fog.PublishedHouse || fog.metrics?.Pending == true)
                throw new InvalidOperationException("Prepared published fog and no pending measurement required.");
            fog.metrics?.Dispose();
            fog.metrics = new BlueprintFogChannels.MetricsProbe(fog.channels, compareBricks, fog.PassPhase, fog.PassSide);
            return "published-metrics-started";
        }
        public static string PublishedMetricsStatus()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = CurrentProbe;
            return fog?.metrics?.Status() ?? "absent";
        }
        public static string SavePublishedMetrics()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = CurrentProbe;
            if (!fog || fog.metrics == null || fog.metrics.Pending) throw new InvalidOperationException("Completed fog metrics required.");
            string result = fog.metrics.Status();
            if (!fog.metrics.Succeeded) throw new InvalidOperationException("Fog metrics failed: " + result);
            string directory = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "BuildWorksQA");
            System.IO.Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "fog-metrics-" + Guid.NewGuid().ToString("N") + ".json");
            System.IO.File.WriteAllText(path, result);
            return path;
        }
        private System.Collections.Generic.IEnumerator<object> wallPass;
        private static BuildWorksTestFog movementOwner;
        private Vector3 passDirection;

        public static string SetPartVisible(int index, bool visible)
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = CurrentProbe;
            if (!fog || fog.borrowedProject != null || fog.building == null || !fog.building.Ready || fog.wallPass != null)
                throw new InvalidOperationException("Prepared idle QA fog building required.");
            fog.building.SetPartVisible(index, visible);
            return HouseStatus();
        }

        public static string RemovePart(int index)
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = CurrentProbe;
            if (!fog || fog.borrowedProject != null || fog.building == null || !fog.building.Ready || fog.wallPass != null)
                throw new InvalidOperationException("Prepared idle QA fog building required.");
            fog.building.RemovePart(index);
            return HouseStatus();
        }

        internal void InitializeWallLine(Player owner)
        {
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab("woodwall") : null;
            if (!prefab || !BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(prefab, out Bounds bounds, true))
                throw new InvalidOperationException("Wall-line QA requires the native wall bounds.");
            bounds = new Bounds(Vector3.Scale(bounds.center, prefab.transform.localScale), Vector3.Scale(bounds.size, prefab.transform.localScale));
            var blueprint = new CompositeBlueprintStore.Blueprint { name = "Transient four-section wall" };
            for (int i = 0; i < 4; ++i)
                blueprint.parts.Add(new CompositeBlueprintStore.Part {
                    prefabName = "woodwall", stableId = "qa-wall-line-" + i,
                    position = new CompositeBlueprintStore.VectorData(new Vector3(i * 2 - 3 - bounds.center.x, -bounds.min.y, -bounds.center.z)),
                    rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity) });
            // Reserve the single-wall transaction allowance for each of the four adjacent sections.
            InitializeBlueprint(owner, blueprint, blockCapacity: 4 * 8192); wallLine = true;
        }

        public static string StartWallPass(int side)
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = CurrentProbe;
            if (!fog || !fog.wallLine || !fog.Active || fog.wallPass != null || side < 0 || side > 3)
                throw new InvalidOperationException("Prepared idle four-section wall and side 0..3 required.");
            fog.PassFailure = null; fog.PassSide = side; fog.wallPass = fog.WallPass(side).GetEnumerator(); movementOwner = fog;
            return "wall-pass-started";
        }

        private System.Collections.Generic.IEnumerable<object> WallPass(int side)
        {
            Vector3 direction = side == 0 ? Vector3.forward : side == 1 ? Vector3.back : side == 2 ? Vector3.right : Vector3.left;
            float distance = borrowedProject != null ? side < 2 ? borrowedDistances.y : borrowedDistances.x : side < 2 ? 2 : 5;
            Vector3 centre = Origin;
            try
            {
                PassPhase = "approach";
                foreach (var step in WalkTo(centre - direction * distance)) yield return step;
                PassPhase = "enter";
                foreach (var step in WalkTo(centre)) yield return step;
                player.SetMoveDir(Vector3.zero); player.SetRun(false); PassPhase = "hold";
                float until = Time.realtimeSinceStartup + 2;
                while (Time.realtimeSinceStartup < until) yield return null;
                PassPhase = "exit";
                foreach (var step in WalkTo(centre + direction * distance)) yield return step;
                player.SetMoveDir(Vector3.zero); player.SetRun(false); PassPhase = "recovery";
                until = Time.realtimeSinceStartup + 8;
                while (Time.realtimeSinceStartup < until) yield return null;
                PassPhase = "complete";
            }
            finally
            {
                if (movementOwner == this) movementOwner = null;
                passDirection = Vector3.zero;
                if (player) { player.SetMoveDir(Vector3.zero); player.SetRun(false); }
            }
        }

        private System.Collections.Generic.IEnumerable<object> WalkTo(Vector3 target)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (true)
            {
                Vector3 delta = target - player.transform.position; delta.y = 0;
                if (delta.magnitude <= .12f) { passDirection = Vector3.zero; yield break; }
                if (Time.realtimeSinceStartup >= deadline) throw new InvalidOperationException("Wall pass did not reach its waypoint; route stopped.");
                passDirection = delta.normalized * Mathf.Min(1, delta.magnitude / .5f);
                yield return null;
            }
        }

        private void FixedUpdate()
        {
            if (wallPass == null) return;
            try
            {
                if (!Active || !player || player != Player.m_localPlayer || player.IsDead() || !network || network != ZNet.instance || Game.instance.IsShuttingDown())
                    throw new InvalidOperationException("Wall pass lost the live QA session.");
                if (borrowedProject != null && (borrowedProject.FogError(borrowedProjectId) != null || borrowedProject.Fog?.Building(borrowedProjectId) != building))
                    throw new InvalidOperationException("The borrowed construction fog changed or failed.");
                if (wallPass.MoveNext()) return;
            }
            catch (Exception error) { PassFailure = error.Message; PassPhase = "failed"; }
            wallPass.Dispose(); wallPass = null;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static class WallPassControls
        {
            private static void Postfix(Player __instance)
            {
                var owner = movementOwner;
                if (!owner || owner.wallPass == null || owner.player != __instance || __instance != Player.m_localPlayer) return;
                // Native SetControls otherwise overwrites world movement later in the same physics tick.
                __instance.SetMoveDir(owner.passDirection); __instance.SetRun(false);
                if (owner.passDirection.sqrMagnitude > .0001f) __instance.SetLookDir(owner.passDirection.normalized);
            }
        }

        internal void Initialize(Player owner, bool pair = false)
        {
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (owner != player) throw new InvalidOperationException("Fog QA player changed.");
            view = GetComponent<Camera>(); capsule = player.GetComponent<CapsuleCollider>();
            if (!view || !capsule || !ZNetScene.instance) throw new InvalidOperationException("Fog QA camera/body/prefab registry missing.");
            GameObject prefab = ZNetScene.instance.GetPrefab("woodwall");
            if (!prefab) throw new InvalidOperationException("Fog QA vanilla woodwall prefab missing.");
            oldDepth = view.depthTextureMode; ownedDepth = oldDepth | DepthTextureMode.Depth; view.depthTextureMode = ownedDepth;
            try
            {
                assets = BlueprintFogAssets.LoadEmbedded();
                if (pair) { channels = new BlueprintFogChannels(); volume = new BlueprintFogVolume(assets); }
                Vector3 position = player.transform.position + Vector3.right * 3;
                if (!BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(prefab, out Bounds prefabBounds, true))
                    throw new InvalidOperationException("Fog QA wall bounds missing.");
                PrefabBounds = prefabBounds;
                Quaternion rotation = Quaternion.Euler(0, 90, 0);
                position.y -= prefabBounds.min.y * prefab.transform.localScale.y;
                // The native prefab pivot need not be the geometry centre; move only the field origin.
                Vector3 origin = position + rotation * Vector3.Scale(prefabBounds.center, prefab.transform.localScale) - Vector3.up * 1.3f;
                Bounds bounds = capsule.bounds;
                visual = new BlueprintFogVisual(assets, prefab, position, rotation, Vector3.one,
                    null, origin, bounds.center, new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y), channels, "QA-wall-A");
                if (pair)
                {
                    Vector3 centre = origin + Vector3.up * 1.3f, scale = new Vector3(1.15f, .9f, 1.1f);
                    Vector3 secondPosition = centre - Vector3.Scale(Vector3.Scale(prefabBounds.center, prefab.transform.localScale), scale);
                    second = new BlueprintFogVisual(assets, prefab, secondPosition, Quaternion.identity, scale,
                        null, origin + new Vector3(.35f, 0, .2f), bounds.center,
                        new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y), channels, "QA-wall-B");
                }
            }
            catch { Cleanup(); throw; }
        }

        internal void InitializeBlueprint(Player owner, CompositeBlueprintStore.Blueprint blueprint, int blockCapacity = 8192)
        {
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (owner != player) throw new InvalidOperationException("Fog QA player changed.");
            view = GetComponent<Camera>(); capsule = player.GetComponent<CapsuleCollider>();
            if (!view || !capsule || !ZNetScene.instance) throw new InvalidOperationException("Fog QA camera/body/prefab registry missing.");
            oldDepth = view.depthTextureMode; ownedDepth = oldDepth | DepthTextureMode.Depth; view.depthTextureMode = ownedDepth;
            try
            {
                assets = BlueprintFogAssets.LoadEmbedded();
                channels = BlueprintFogChannels.CreateArena(assets, blueprint.parts.Count, blockCapacity, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D);
                Bounds body = capsule.bounds;
                building = new BlueprintFogBuilding(assets, channels, blueprint, name => ZNetScene.instance.GetPrefab(name),
                    player.transform.position + Vector3.right * 6, Quaternion.identity, body.center,
                    new Vector2(Mathf.Max(body.extents.x, body.extents.z), body.extents.y), deferPreparation: true);
                preparation = building.Prepare().GetEnumerator(); preparationStarted = Time.realtimeSinceStartup;
            }
            catch { Cleanup(); throw; }
        }

        private void OnPreCull()
        {
            if (borrowedProject != null || !Active || !enabled) return;
            try { building?.PrepareRender(); }
            catch (Exception error) { Fail(error); }
        }

        private void Update()
        {
            if (borrowedProject != null) return;
            if (!Active && preparation == null) return;
            if (!player || player != Player.m_localPlayer || !network || network != ZNet.instance ||
                player.IsDead() || !Game.instance || Game.instance.IsShuttingDown())
            { Destroy(this); return; }
            if (preparation != null)
            {
                try
                {
                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    bool more = preparation.MoveNext(); ++preparationSteps;
                    maxPreparationStepMilliseconds = Math.Max(maxPreparationStepMilliseconds,
                        (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency);
                    if (!more)
                    {
                        preparation.Dispose(); preparation = null;
                        preparationMilliseconds = (Time.realtimeSinceStartup - preparationStarted) * 1000d; clock = 0;
                    }
                }
                catch (Exception error) { Cleanup(); Fail(error); }
                return;
            }
            if (SkipSimulation) return;
            try
            {
                clock += Mathf.Min(Time.deltaTime, BlueprintFogSimulation.Step * 8);
                Bounds bounds = capsule.bounds;
                visual?.AdvanceTo(clock, bounds.center, new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y), true);
                second?.AdvanceTo(clock, bounds.center, new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y), true);
                if (building != null)
                {
                    if (Mathf.FloorToInt(clock / BlueprintFogSimulation.Step + .0001f) >
                        Mathf.FloorToInt(building.Clock / BlueprintFogSimulation.Step + .0001f))
                        building.AdvanceTo(clock, bounds.center, new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y), true);
                    else building.Pump();
                }
                PeakFieldBytes = Math.Max(PeakFieldBytes, FieldBytes);
            }
            catch (Exception error) { Fail(error); }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture target)
        {
            if (borrowedProject != null) { Graphics.Blit(source, target); return; }
            if (!Active || SkipVolume) { Graphics.Blit(source, target); return; }
            try
            {
                Light sun = RenderSettings.sun;
                Vector3 direction = sun ? sun.transform.forward : Vector3.down;
                if (building != null) building.Render(view, source, target, player.transform.position - direction * 8,
                    direction, sun ? Mathf.Max(0, sun.intensity) * 1.6f : 0);
                else if (volume != null) volume.Render(view, source, target, visual, second, visual.Origin - direction * 8,
                    direction, sun ? Mathf.Max(0, sun.intensity) * 1.6f : 0);
                else visual.RenderVolume(view, source, target, visual.Origin - direction * 8,
                    direction, sun ? Mathf.Max(0, sun.intensity) * 1.6f : 0);
                ++RenderFrames;
            }
            catch (Exception error) { Fail(error); Graphics.Blit(source, target); }
        }

        private void Fail(Exception error)
        {
            Failure = error.Message;
            firstFault = firstFault ?? channels?.FirstSnapshotFault;
            if (wallPass != null) { PassFailure = Failure; PassPhase = "failed"; }
            Debug.LogError("BuildWorks fog QA stopped: " + error + "\nFirst GPU fault: " + JsonUtility.ToJson(firstFault)); enabled = false;
        }
        private BlueprintFogChannels.SnapshotFault firstFault;
        private string FirstFaultJson => (firstFault ?? channels?.FirstSnapshotFault) is BlueprintFogChannels.SnapshotFault fault ? JsonUtility.ToJson(fault) : "";
        public float SampleSurfaceDamage() => SampleAlpha(visual?.Simulation?.Surface);
        public float SampleSmokeDensity() => SampleAlpha(visual?.Simulation?.Smoke);
        public float SampleSecondSurfaceDamage() => SampleAlpha(second?.Simulation?.Surface);
        public float SampleSecondSmokeDensity() => SampleAlpha(second?.Simulation?.Smoke);
        private static float SampleAlpha(RenderTexture field)
        {
            if (!field) throw new InvalidOperationException("Fog QA field missing.");
            var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            if (request.hasError) throw new InvalidOperationException("Fog QA readback failed.");
            float maximum = 0; int count = 0;
            for (int layer = 0; layer < request.layerCount; ++layer)
            {
                var values = request.GetData<float>(layer); count = checked(count + values.Length);
                for (int i = 3; i < values.Length; i += 4)
                {
                    if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new InvalidOperationException("Nonfinite fog QA field.");
                    maximum = Mathf.Max(maximum, values[i]);
                }
            }
            if (count != field.width * field.height * field.volumeDepth * 4) throw new InvalidOperationException("Incomplete fog QA field readback.");
            return maximum;
        }
        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            metrics?.Dispose();
            if (movementOwner == this) movementOwner = null;
            passDirection = Vector3.zero;
            var movement = wallPass; wallPass = null; movement?.Dispose();
            var pending = preparation; preparation = null;
            // The project alone owns its renderer and GPU pool; this probe borrows only movement/measurement access.
            if (borrowedProject != null) { building = null; channels = null; borrowedProject = null; }
            if (borrowedProbe == this) borrowedProbe = null;
            try { pending?.Dispose(); }
            finally
            {
            building?.Dispose(); building = null;
            visual?.Dispose(); visual = null; second?.Dispose(); second = null;
            volume?.Dispose(); volume = null; channels?.Dispose(); channels = null; assets?.Dispose(); assets = null;
            if (view && view.depthTextureMode == ownedDepth) view.depthTextureMode = oldDepth;
            }
        }
    }
}
#endif

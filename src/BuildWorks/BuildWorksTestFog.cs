#if BUILDWORKS_TEST_STAND
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed class BuildWorksTestFog : MonoBehaviour
    {
        public static string HouseStatus()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = Camera.main ? Camera.main.GetComponent<BuildWorksTestFog>() : null;
            return JsonUtility.ToJson(new HouseReceipt {
                active = fog && fog.PublishedHouse, failure = fog?.Failure,
                parts = fog?.PartCount ?? 0, owners = fog?.ActiveOwners ?? 0, residentOwners = fog?.ResidentOwners ?? 0,
                queuedSamples = fog?.building?.QueuedSamples ?? 0, minimumTick = fog?.building?.MinimumConfirmedTick ?? 0,
                lastFlags = fog?.building?.LastFlags ?? 0, frames = fog?.RenderFrames ?? 0,
                fieldBytes = fog?.FieldBytes ?? 0, renderBytes = fog?.AtlasBytes ?? 0, velocityRamBytes = fog?.VelocityRamBytes ?? 0,
                origin = fog?.Origin ?? Vector3.zero, clock = fog?.clock ?? 0,
                processMemory = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 });
        }
        [Serializable] private sealed class HouseReceipt
        {
            public bool active;
            public string failure;
            public int parts, owners, residentOwners, queuedSamples, minimumTick, frames;
            public uint lastFlags;
            public long fieldBytes, renderBytes, velocityRamBytes, processMemory;
            public Vector3 origin;
            public float clock;
        }
        // GABS can call handlers but cannot retain an internal constructor result for this resource-only probe.
        public static string VerifyPublication()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,
                "..", "..", "Default", "BuildWorks", "artifacts", "fog-runtime-assets-candidate", "buildworks-fog-v11"));
            var result = new PublicationReceipt();
            RenderTexture pages;
            using (var probeAssets = new BlueprintFogAssets(System.IO.File.ReadAllBytes(path)))
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
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,
                "..", "..", "Default", "BuildWorks", "artifacts", "fog-runtime-assets-candidate", "buildworks-fog-v11"));
            var shape = new Texture3D(4, 4, 4, TextureFormat.RGBAHalf, false);
            var result = new VelocityReceipt(); Texture3D upload;
            try
            {
                shape.SetPixelData(new ushort[256], 0); shape.Apply(false, false);
                using (var probeAssets = new BlueprintFogAssets(System.IO.File.ReadAllBytes(path)))
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
        private BlueprintFogChannels channels;
        private BlueprintFogVolume volume;
        private Player player;
        private ZNet network;
        private CapsuleCollider capsule;
        private Camera view;
        private DepthTextureMode oldDepth, ownedDepth;
        private float clock;
        public int RenderFrames { get; private set; }
        public int Tick => visual?.Simulation?.Tick ?? (building == null ? 0 : Mathf.FloorToInt(building.Clock / BlueprintFogSimulation.Step + .0001f));
        public Vector3 Origin => visual?.Origin ?? building?.Origin ?? Vector3.zero;
        public bool Active => visual != null || building != null;
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
        public string Failure { get; private set; }
        public Bounds PrefabBounds { get; private set; }

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

        internal void InitializeBlueprint(Player owner, CompositeBlueprintStore.Blueprint blueprint)
        {
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (owner != player) throw new InvalidOperationException("Fog QA player changed.");
            view = GetComponent<Camera>(); capsule = player.GetComponent<CapsuleCollider>();
            if (!view || !capsule || !ZNetScene.instance) throw new InvalidOperationException("Fog QA camera/body/prefab registry missing.");
            oldDepth = view.depthTextureMode; ownedDepth = oldDepth | DepthTextureMode.Depth; view.depthTextureMode = ownedDepth;
            try
            {
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,
                    "..", "..", "Default", "BuildWorks", "artifacts", "fog-runtime-assets-candidate", "buildworks-fog-v11"));
                assets = new BlueprintFogAssets(System.IO.File.ReadAllBytes(path));
                channels = BlueprintFogChannels.CreateArena(assets, blueprint.parts.Count, 8192);
                Bounds body = capsule.bounds;
                building = new BlueprintFogBuilding(assets, channels, blueprint, name => ZNetScene.instance.GetPrefab(name),
                    player.transform.position + Vector3.right * 6, Quaternion.identity, body.center,
                    new Vector2(Mathf.Max(body.extents.x, body.extents.z), body.extents.y));
                building.PrepareRender();
            }
            catch { Cleanup(); throw; }
        }

        private void OnPreCull()
        {
            if (!Active || !enabled) return;
            try { building?.PrepareRender(); }
            catch (Exception error) { Fail(error); }
        }

        private void Update()
        {
            if (!Active) return;
            if (!player || player != Player.m_localPlayer || !network || network != ZNet.instance ||
                player.IsDead() || !Game.instance || Game.instance.IsShuttingDown())
            { Destroy(this); return; }
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
        { Failure = error.Message; Debug.LogError("BuildWorks fog QA stopped: " + Failure); enabled = false; }
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
            building?.Dispose(); building = null;
            visual?.Dispose(); visual = null; second?.Dispose(); second = null;
            volume?.Dispose(); volume = null; channels?.Dispose(); channels = null; assets?.Dispose(); assets = null;
            if (view && view.depthTextureMode == ownedDepth) view.depthTextureMode = oldDepth;
        }
    }
}
#endif

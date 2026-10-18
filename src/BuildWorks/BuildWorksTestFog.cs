#if BUILDWORKS_TEST_STAND
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed class BuildWorksTestFog : MonoBehaviour
    {
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
                assets = BlueprintFogAssets.LoadEmbedded(); channels = new BlueprintFogChannels(32, BlueprintFogChannels.MaximumFieldBudget, true);
                Bounds body = capsule.bounds;
                building = new BlueprintFogBuilding(assets, channels, blueprint, name => ZNetScene.instance.GetPrefab(name),
                    player.transform.position + Vector3.right * 6, Quaternion.identity, body.center,
                    new Vector2(Mathf.Max(body.extents.x, body.extents.z), body.extents.y));
            }
            catch { Cleanup(); throw; }
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
                building?.AdvanceTo(clock, bounds.center, new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y), true);
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

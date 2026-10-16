#if BUILDWORKS_TEST_STAND
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed class BuildWorksTestFog : MonoBehaviour
    {
        private BlueprintFogAssets assets;
        private BlueprintFogVisual visual;
        private Player player;
        private ZNet network;
        private CapsuleCollider capsule;
        private Camera view;
        private DepthTextureMode oldDepth, ownedDepth;
        private float clock;
        public int RenderFrames { get; private set; }
        public int Tick => visual?.Simulation?.Tick ?? 0;
        public Vector3 Origin => visual?.Origin ?? Vector3.zero;
        public bool Active => visual != null;
        public string Failure { get; private set; }
        public Bounds PrefabBounds { get; private set; }

        internal void Initialize(Player owner)
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
                    null, origin, bounds.center, new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y));
            }
            catch { Cleanup(); throw; }
        }

        private void Update()
        {
            if (visual == null) return;
            if (!player || player != Player.m_localPlayer || !network || network != ZNet.instance ||
                player.IsDead() || !Game.instance || Game.instance.IsShuttingDown())
            { Destroy(this); return; }
            try
            {
                clock += Mathf.Min(Time.deltaTime, BlueprintFogSimulation.Step * 8);
                Bounds bounds = capsule.bounds;
                visual.AdvanceTo(clock, bounds.center, new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y), true);
            }
            catch (Exception error) { Fail(error); }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture target)
        {
            if (visual == null) { Graphics.Blit(source, target); return; }
            try
            {
                Light sun = RenderSettings.sun;
                Vector3 direction = sun ? sun.transform.forward : Vector3.down;
                visual.RenderVolume(view, source, target, visual.Origin - direction * 8,
                    direction, sun ? Mathf.Max(0, sun.intensity) * 1.6f : 0);
                ++RenderFrames;
            }
            catch (Exception error) { Fail(error); Graphics.Blit(source, target); }
        }

        private void Fail(Exception error)
        { Failure = error.Message; Debug.LogError("BuildWorks fog QA stopped: " + Failure); enabled = false; }
        public float SampleSurfaceDamage() => SampleAlpha(visual?.Simulation?.Surface);
        public float SampleSmokeDensity() => SampleAlpha(visual?.Simulation?.Smoke);
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
            visual?.Dispose(); visual = null; assets?.Dispose(); assets = null;
            if (view && view.depthTextureMode == ownedDepth) view.depthTextureMode = oldDepth;
        }
    }
}
#endif

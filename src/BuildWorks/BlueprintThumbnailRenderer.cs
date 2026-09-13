using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed class BlueprintThumbnailRenderer : IDisposable
    {
        private const int TextureSize = 256;
        private const int ThumbnailLayer = 31;
        private static readonly Color ThumbnailBackground =
            new Color(0.04f, 0.015f, 0.10f, 0f);
        private static readonly Color PreviewBackground =
            new Color(0.16f, 0.18f, 0.20f, 1f);
        private static readonly Type ImageConversionType = ResolveImageConversionType();
        private static readonly MethodInfo LoadImageMethod = ImageConversionType?.GetMethod(
            "LoadImage",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
            null);
        private static readonly MethodInfo EncodePngMethod = ImageConversionType?.GetMethod(
            "EncodeToPNG",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Texture2D) },
            null);
        private readonly string directory = Path.Combine(
            Paths.ConfigPath,
            "BuildWorks",
            "thumbnails");
        private readonly Dictionary<string, Sprite> sprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> failures =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<PendingThumbnail> pending = new Queue<PendingThumbnail>();
        private readonly HashSet<string> queued = new HashSet<string>(StringComparer.Ordinal);

        private static Type ResolveImageConversionType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("UnityEngine.ImageConversion", false);
                if (type != null) return type;
            }
            try
            {
                return Assembly.Load("UnityEngine.ImageConversionModule")
                    ?.GetType("UnityEngine.ImageConversion", false);
            }
            catch
            {
                return null;
            }
        }

        public Sprite GetOrQueue(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces)
        {
            if (blueprint == null || string.IsNullOrWhiteSpace(blueprint.id) ||
                blueprint.parts == null || pieces == null ||
                blueprint.parts.Count != pieces.Count)
                return null;
            if (sprites.TryGetValue(blueprint.id, out Sprite cached)) return cached;
            if (failures.Contains(blueprint.id)) return null;

            Sprite loaded = Load(blueprint.id);
            if (loaded)
            {
                sprites.Add(blueprint.id, loaded);
                return loaded;
            }
            if (queued.Add(blueprint.id))
                pending.Enqueue(new PendingThumbnail(blueprint, pieces));
            return null;
        }

        public bool ProcessOne(out string blueprintId, out Sprite sprite)
        {
            blueprintId = null;
            sprite = null;
            while (pending.Count > 0)
            {
                PendingThumbnail item = pending.Dequeue();
                if (item.Blueprint == null) continue;
                blueprintId = item.Blueprint.id;
                if (!queued.Remove(blueprintId)) continue;
                if (failures.Contains(blueprintId)) continue;
                sprite = RenderAndStore(item.Blueprint, item.Pieces);
                return sprite;
            }
            return false;
        }

        public Sprite Refresh(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces)
        {
            if (blueprint == null || string.IsNullOrWhiteSpace(blueprint.id)) return null;
            RemoveCached(blueprint.id);
            failures.Remove(blueprint.id);
            queued.Remove(blueprint.id);
            return RenderAndStore(blueprint, pieces);
        }

        public void Delete(string blueprintId)
        {
            if (string.IsNullOrWhiteSpace(blueprintId)) return;
            RemoveCached(blueprintId);
            failures.Remove(blueprintId);
            queued.Remove(blueprintId);
            string path = ThumbnailPath(blueprintId);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not remove blueprint thumbnail: " +
                    exception.Message);
            }
        }

        public PreviewSession BeginPreview(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces)
        {
            if (blueprint == null || blueprint.parts == null || pieces == null ||
                blueprint.parts.Count != pieces.Count) return null;
            try
            {
                return new PreviewSession(blueprint, pieces);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not open blueprint preview: " + exception);
                return null;
            }
        }

        private Sprite RenderAndStore(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces)
        {
            if (blueprint == null || blueprint.parts == null || pieces == null ||
                blueprint.parts.Count != pieces.Count) return null;

            Sprite rendered = null;
            string temporary = null;
            try
            {
                rendered = Render(blueprint, pieces);
                if (!rendered)
                {
                    failures.Add(blueprint.id);
                    Debug.LogWarning("BuildWorks could not render blueprint thumbnail: " +
                        "the render contained no visible model pixels.");
                    return null;
                }
                Directory.CreateDirectory(directory);
                string path = ThumbnailPath(blueprint.id);
                temporary = path + ".tmp";
                byte[] png = EncodePngMethod?.Invoke(
                    null,
                    new object[] { rendered.texture }) as byte[];
                if (png == null || png.Length == 0)
                    throw new InvalidDataException("PNG encoder is unavailable");
                File.WriteAllBytes(temporary, png);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
                sprites[blueprint.id] = rendered;
                return rendered;
            }
            catch (Exception exception)
            {
                try
                {
                    if (!string.IsNullOrEmpty(temporary) && File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning("BuildWorks could not remove temporary thumbnail: " +
                        cleanupException.Message);
                }
                if (rendered)
                {
                    Texture2D texture = rendered.texture;
                    UnityEngine.Object.Destroy(rendered);
                    if (texture) UnityEngine.Object.Destroy(texture);
                }
                failures.Add(blueprint.id);
                Debug.LogWarning("BuildWorks could not render blueprint thumbnail: " + exception);
                return null;
            }
        }

        public void Dispose()
        {
            foreach (Sprite sprite in sprites.Values)
            {
                if (!sprite) continue;
                Texture2D texture = sprite.texture;
                UnityEngine.Object.Destroy(sprite);
                if (texture) UnityEngine.Object.Destroy(texture);
            }
            sprites.Clear();
            failures.Clear();
            pending.Clear();
            queued.Clear();
        }

        private Sprite Load(string blueprintId)
        {
            string path = ThumbnailPath(blueprintId);
            if (!File.Exists(path)) return null;
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "BuildWorks_BlueprintThumbnail_" + blueprintId
                };
                bool loaded = LoadImageMethod != null &&
                    (bool)LoadImageMethod.Invoke(
                        null,
                        new object[] { texture, File.ReadAllBytes(path), false });
                if (!loaded)
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                if (HasLegacyOpaqueBackground(texture))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = "BuildWorks_BlueprintIcon_" + blueprintId;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not load blueprint thumbnail: " + exception);
                return null;
            }
        }

        private void RemoveCached(string blueprintId)
        {
            if (!sprites.TryGetValue(blueprintId, out Sprite sprite)) return;
            sprites.Remove(blueprintId);
            if (!sprite) return;
            Texture2D texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture) UnityEngine.Object.Destroy(texture);
        }

        private string ThumbnailPath(string blueprintId) => Path.Combine(
            directory,
            Path.GetFileName(blueprintId) + ".png");

        private static Sprite Render(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces)
        {
            GameObject root = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture target = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                root = new GameObject("BuildWorks_BlueprintThumbnail")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = ThumbnailLayer
                };
                for (int index = 0; index < pieces.Count; ++index)
                {
                    Piece piece = pieces[index];
                    if (!piece) return null;
                    GameObject visual = PlacementGhostPreviewView.CreateVisualClone(
                        piece.gameObject,
                        "Part_" + index,
                        ThumbnailLayer);
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = blueprint.parts[index].position.ToVector3();
                    visual.transform.localRotation = blueprint.parts[index].rotation.ToQuaternion();
                    visual.transform.localScale = Vector3.Scale(
                        piece.transform.lossyScale, blueprint.parts[index].scale.ToVector3());
                    visual.SetActive(true);
                }

                if (!TryGetBounds(root, out Bounds bounds)) return null;
                cameraObject = new GameObject("BuildWorks_BlueprintThumbnailCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = ThumbnailLayer
                };
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = ThumbnailBackground;
                camera.cullingMask = 1 << ThumbnailLayer;
                camera.orthographic = true;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                Vector3 cameraDirection = Quaternion.Euler(
                    blueprint.previewPitch,
                    blueprint.previewYaw,
                    0f) * Vector3.back;
                float distance = Mathf.Max(6f, bounds.size.magnitude * 2f);
                camera.transform.position = bounds.center + cameraDirection * distance;
                camera.transform.LookAt(bounds.center, Vector3.up);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 4f + bounds.size.magnitude;
                camera.orthographicSize = RequiredOrthographicSize(camera.transform, bounds) *
                    1.18f / Mathf.Clamp(blueprint.previewZoom, 0.5f, 3f);

                lightObject = new GameObject("BuildWorks_BlueprintThumbnailLight")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = ThumbnailLayer
                };
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.91f, 0.78f, 1f);
                light.intensity = 1.25f;
                light.cullingMask = 1 << ThumbnailLayer;
                light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

                target = new RenderTexture(
                    TextureSize,
                    TextureSize,
                    24,
                    RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                target.Create();
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                Texture2D texture = new Texture2D(
                    TextureSize,
                    TextureSize,
                    TextureFormat.RGBA32,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "BuildWorks_BlueprintThumbnail_" + blueprint.id
                };
                texture.ReadPixels(new Rect(0f, 0f, TextureSize, TextureSize), 0, 0);
                texture.Apply(false, false);
                if (!MakeBackgroundTransparent(texture))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, TextureSize, TextureSize),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = "BuildWorks_BlueprintIcon_" + blueprint.id;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (target)
                {
                    target.Release();
                    UnityEngine.Object.Destroy(target);
                }
                if (cameraObject) UnityEngine.Object.Destroy(cameraObject);
                if (lightObject) UnityEngine.Object.Destroy(lightObject);
                if (root) UnityEngine.Object.Destroy(root);
            }
        }

        private static bool MakeBackgroundTransparent(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            if (pixels.Length == 0) return false;
            int width = texture.width;
            int height = texture.height;
            Color32 topLeft = pixels[(height - 1) * width];
            Color32 topRight = pixels[pixels.Length - 1];
            Color32 bottomLeft = pixels[0];
            Color32 bottomRight = pixels[width - 1];
            var background = new Color32(
                (byte)((topLeft.r + topRight.r + bottomLeft.r + bottomRight.r) / 4),
                (byte)((topLeft.g + topRight.g + bottomLeft.g + bottomRight.g) / 4),
                (byte)((topLeft.b + topRight.b + bottomLeft.b + bottomRight.b) / 4),
                0);
            bool visible = false;
            for (int index = 0; index < pixels.Length; ++index)
            {
                Color32 pixel = pixels[index];
                int difference = Math.Abs(pixel.r - background.r) +
                    Math.Abs(pixel.g - background.g) +
                    Math.Abs(pixel.b - background.b);
                int derivedAlpha = Mathf.Clamp((difference - 12) * 6, 0, 255);
                int alpha = Math.Max(pixel.a, derivedAlpha);
                pixel.a = (byte)alpha;
                pixels[index] = pixel;
                visible |= alpha > 24;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return visible;
        }

        private static bool HasLegacyOpaqueBackground(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            if (pixels.Length == 0) return false;
            int width = texture.width;
            int height = texture.height;
            return IsOpaqueDark(pixels[0]) &&
                IsOpaqueDark(pixels[width - 1]) &&
                IsOpaqueDark(pixels[(height - 1) * width]) &&
                IsOpaqueDark(pixels[pixels.Length - 1]);
        }

        private static bool IsOpaqueDark(Color32 pixel) =>
            pixel.a > 240 && pixel.r < 24 && pixel.g < 24 && pixel.b < 24;

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found && bounds.size.sqrMagnitude > 0.0001f;
        }

        private static float RequiredOrthographicSize(Transform camera, Bounds bounds)
        {
            Vector3 extents = bounds.extents;
            float halfWidth = 0f;
            float halfHeight = 0f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 offset = new Vector3(
                            extents.x * x,
                            extents.y * y,
                            extents.z * z);
                        halfWidth = Mathf.Max(halfWidth,
                            Mathf.Abs(Vector3.Dot(offset, camera.right)));
                        halfHeight = Mathf.Max(halfHeight,
                            Mathf.Abs(Vector3.Dot(offset, camera.up)));
                    }
                }
            }
            return Mathf.Max(0.5f, halfWidth, halfHeight);
        }

        private readonly struct PendingThumbnail
        {
            public PendingThumbnail(
                CompositeBlueprintStore.Blueprint blueprint,
                IReadOnlyList<Piece> pieces)
            {
                Blueprint = blueprint;
                Pieces = new List<Piece>(pieces);
            }

            public CompositeBlueprintStore.Blueprint Blueprint { get; }
            public IReadOnlyList<Piece> Pieces { get; }
        }

        internal sealed class PreviewSession : IDisposable
        {
            private const int PreviewSize = 512;
            private readonly GameObject root;
            private readonly GameObject cameraObject;
            private readonly GameObject lightObject;
            private readonly Camera camera;
            private readonly Bounds bounds;
            private readonly float distance;

            public PreviewSession(
                CompositeBlueprintStore.Blueprint blueprint,
                IReadOnlyList<Piece> pieces)
            {
                root = new GameObject("BuildWorks_BlueprintPreview")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = ThumbnailLayer
                };
                try
                {
                    for (int index = 0; index < pieces.Count; ++index)
                    {
                        Piece piece = pieces[index];
                        if (!piece)
                            throw new InvalidOperationException("Blueprint piece is missing");
                        GameObject visual = PlacementGhostPreviewView.CreateVisualClone(
                            piece.gameObject,
                            "Part_" + index,
                            ThumbnailLayer);
                        visual.transform.SetParent(root.transform, false);
                        visual.transform.localPosition = blueprint.parts[index].position.ToVector3();
                        visual.transform.localRotation = blueprint.parts[index].rotation.ToQuaternion();
                        visual.transform.localScale = Vector3.Scale(
                            piece.transform.lossyScale, blueprint.parts[index].scale.ToVector3());
                        visual.SetActive(true);
                    }
                    if (!TryGetBounds(root, out Bounds sceneBounds))
                        throw new InvalidOperationException("Blueprint has no visible meshes");
                    bounds = sceneBounds;

                    cameraObject = new GameObject("BuildWorks_BlueprintPreviewCamera")
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        layer = ThumbnailLayer
                    };
                    camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = PreviewBackground;
                    camera.cullingMask = 1 << ThumbnailLayer;
                    camera.orthographic = true;
                    camera.allowHDR = false;
                    camera.allowMSAA = true;

                    lightObject = new GameObject("BuildWorks_BlueprintPreviewLight")
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        layer = ThumbnailLayer
                    };
                    Light light = lightObject.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.color = new Color(1f, 0.91f, 0.78f, 1f);
                    light.intensity = 1.25f;
                    light.cullingMask = 1 << ThumbnailLayer;
                    light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

                    Texture = new RenderTexture(
                        PreviewSize,
                        PreviewSize,
                        24,
                        RenderTextureFormat.ARGB32)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        antiAliasing = 4,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    Texture.Create();
                    camera.targetTexture = Texture;
                    distance = Mathf.Max(6f, bounds.size.magnitude * 2f);
                    camera.nearClipPlane = 0.01f;
                    camera.farClipPlane = distance * 4f + bounds.size.magnitude;
                    Render(blueprint.previewYaw, blueprint.previewPitch, blueprint.previewZoom);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public RenderTexture Texture { get; }

            public void Render(float yaw, float pitch, float zoom)
            {
                Vector3 direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
                camera.transform.position = bounds.center + direction * distance;
                camera.transform.LookAt(bounds.center, Vector3.up);
                camera.orthographicSize = RequiredOrthographicSize(camera.transform, bounds) *
                    1.18f / Mathf.Clamp(zoom, 0.5f, 3f);
                camera.Render();
            }

            public void Dispose()
            {
                if (Texture)
                {
                    Texture.Release();
                    UnityEngine.Object.Destroy(Texture);
                }
                if (cameraObject) UnityEngine.Object.Destroy(cameraObject);
                if (lightObject) UnityEngine.Object.Destroy(lightObject);
                if (root) UnityEngine.Object.Destroy(root);
            }
        }
    }
}

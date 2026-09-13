using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed class BlueprintEditorIconLibrary : IDisposable
    {
        private const string ResourcePrefix =
            "OstrixMods.BuildWorks.Assets.BlueprintEditorIcons.";
        private static readonly MethodInfo LoadImageMethod = ResolveLoadImageMethod();
        private readonly Dictionary<string, Sprite> sprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failures =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Texture2D> resourceTextures = new HashSet<Texture2D>();

        internal Sprite Get(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName)) return null;
            if (sprites.TryGetValue(iconName, out Sprite cached)) return cached;
            if (failures.Contains(iconName)) return null;

            Assembly assembly = typeof(BlueprintEditorIconLibrary).Assembly;
            string resourceName = ResourcePrefix + iconName + ".png";
            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Texture2D resource = Resources.Load<Texture2D>("BuildWorks/Icons/" + iconName);
                        if (!resource) { failures.Add(iconName); return null; }
                        texture = resource;
                        resourceTextures.Add(resource);
                    }
                    else
                    {
                        if (LoadImageMethod == null) { failures.Add(iconName); return null; }
                        var bytes = new byte[stream.Length];
                        int offset = 0;
                        while (offset < bytes.Length)
                        {
                            int read = stream.Read(bytes, offset, bytes.Length - offset);
                            if (read <= 0) break;
                            offset += read;
                        }
                        if (offset != bytes.Length)
                        {
                            failures.Add(iconName);
                            return null;
                        }

                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                        {
                            name = "BuildWorks_EditorIconTexture_" + iconName,
                            hideFlags = HideFlags.HideAndDontSave,
                            filterMode = FilterMode.Bilinear,
                            wrapMode = TextureWrapMode.Clamp
                        };
                        bool loaded = (bool)LoadImageMethod.Invoke(
                            null, new object[] { texture, bytes, true });
                        if (!loaded)
                        {
                            UnityEngine.Object.Destroy(texture);
                            texture = null;
                            failures.Add(iconName);
                            return null;
                        }
                    }

                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    sprite.name = "BuildWorks_EditorIcon_" + iconName;
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                    sprites.Add(iconName, sprite);
                    return sprite;
                }
            }
            catch (Exception exception)
            {
                if (sprite) UnityEngine.Object.Destroy(sprite);
                if (texture && !resourceTextures.Contains(texture)) UnityEngine.Object.Destroy(texture);
                failures.Add(iconName);
                Debug.LogWarning("BuildWorks could not load editor icon " +
                    iconName + ": " + exception.Message);
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
                if (texture && !resourceTextures.Contains(texture)) UnityEngine.Object.Destroy(texture);
            }
            sprites.Clear();
            failures.Clear();
            resourceTextures.Clear();
        }

        private static MethodInfo ResolveLoadImageMethod()
        {
            Type type = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("UnityEngine.ImageConversion", false);
                if (type != null) break;
            }
            if (type == null)
            {
                try
                {
                    type = Assembly.Load("UnityEngine.ImageConversionModule")
                        ?.GetType("UnityEngine.ImageConversion", false);
                }
                catch
                {
                    return null;
                }
            }
            return type.GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);
        }
    }
}

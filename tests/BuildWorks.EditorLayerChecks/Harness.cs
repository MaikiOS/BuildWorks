using System;
using UnityEngine;
namespace UnityEngine
{
    public class Object { public bool Alive = true; public static implicit operator bool(Object o) => o != null && o.Alive; }
    public struct Scene { public bool Valid; public bool IsValid() => Valid; }
    public class GameObject : Object { public int layer; public Scene scene; }
    public class Component : Object
    {
        public GameObject Owner;
        public bool ThrowOnOwner;
        public GameObject gameObject => ThrowOnOwner ? throw new Exception("Non-renderable component accessed") : Owner;
    }
    public class Renderer : Component { }
    public class CanvasRenderer : Component { }
    public class Terrain : Component { }
    public class Projector : Component { }
    namespace VFX { public class VisualEffect : Component { } }
    public static class Resources
    {
        public static Component[] All;
        public static T[] FindObjectsOfTypeAll<T>() => (T[])(object)All;
    }
}
internal static class BlueprintThumbnailRenderer { internal const int ThumbnailLayer = 30; }
public static class EditorLayerChecks
{
// PRODUCTION_METHODS
    private static GameObject Owner(int layer, bool valid = true) => new GameObject { layer = layer, scene = new Scene { Valid = valid } };
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        Resources.All = new Component[] { new Renderer { Owner = null } };
        Require(FindUnusedEditorLayer() == 31, "Ownerless renderer must be skipped");
        Resources.All = new Component[] { null, new Component { ThrowOnOwner = true },
            new Renderer { Alive = false }, new Renderer { Owner = new GameObject { Alive = false, layer = 31 } },
            new Renderer { Owner = Owner(31, false) } };
        Require(FindUnusedEditorLayer() == 31, "Dead, non-renderable and prefab components must be skipped");
        Resources.All = new Component[] { new Renderer { Owner = Owner(31) }, new CanvasRenderer { Owner = Owner(29) },
            new Terrain { Owner = Owner(28) }, new Projector { Owner = Owner(27) },
            new UnityEngine.VFX.VisualEffect { Owner = Owner(26) } };
        Require(FindUnusedEditorLayer() == 25, "All renderable kinds and thumbnail layer must stay reserved");
        Resources.All = new Component[24];
        for (int i = 0; i < 24; ++i) Resources.All[i] = new Renderer { Owner = Owner(i + 8) };
        bool refused = false;
        try { FindUnusedEditorLayer(); } catch (InvalidOperationException) { refused = true; }
        Require(refused, "Exhausted layers must fail without borrowing a used layer");
        Console.WriteLine("PASS production editor layer scan: ownerless/dead/non-renderable/prefab, five renderable kinds, reserved/exhausted layers");
    }
}

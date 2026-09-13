// Only external host types. Editor behavior comes from copied, hash-verified product sources.
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour { public static Player m_localPlayer; }
namespace BepInEx
{
    public static class Paths { public static string ConfigPath => Application.temporaryCachePath; }
}
namespace OstrixMods.BuildWorks
{
    // Stable production IDs required by the isolated editor catalog model.
    internal static class HammerCatalogOrganizer
    {
        internal const string OtherGroup = "other";
        internal const string VanillaSource = "vanilla";
    }

    internal class PrecisionPlacementSession
    {
        internal readonly struct TransformSnapshot
        {
            public TransformSnapshot(Vector3 position, Quaternion rotation, Vector3? scale = null)
            { Position = position; Rotation = rotation; Scale = scale ?? Vector3.one; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }
    }
}

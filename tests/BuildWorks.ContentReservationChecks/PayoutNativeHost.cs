using System.Collections.Generic;
namespace ReservationChecks
{
    internal static class Version { internal enum Item : byte { Current=110 } }
    internal static class Time { internal static int frameCount; }
    internal sealed class ObjectDB : Obj
    {
        internal static readonly ObjectDB instance=new ObjectDB();
        private readonly Dictionary<string,Prefab> prefabs=new Dictionary<string,Prefab>();
        internal Prefab GetItemPrefab(string name){if(!prefabs.TryGetValue(name,out var prefab)){prefab=new Prefab{name=name};prefabs.Add(name,prefab);}return prefab;}
    }
}

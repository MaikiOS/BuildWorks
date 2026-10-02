using System.Collections.Generic;
using UnityEngine;
public enum GlobalKeys { NoBuildCost, NoCraftCost }
public class ZoneSystem : MonoBehaviour
{
    public static ZoneSystem instance;
    public HashSet<GlobalKeys> Keys = new HashSet<GlobalKeys>();
    public bool GetGlobalKey(GlobalKeys key) => Keys.Contains(key);
}

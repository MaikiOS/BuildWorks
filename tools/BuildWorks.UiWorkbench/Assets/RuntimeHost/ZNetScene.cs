using System.Collections.Generic;
using UnityEngine;

// Isolated prefab lookup contract; real hash resolution is verified separately in Valheim.
public sealed class ZNetScene : MonoBehaviour
{
    public static ZNetScene instance;
    public readonly Dictionary<int,GameObject> Prefabs=new Dictionary<int,GameObject>();
    public GameObject GetPrefab(int hash)=>Prefabs.TryGetValue(hash,out var prefab)?prefab:null;
}

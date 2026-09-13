using System.Collections.Generic;
using UnityEngine;

// External host contract only. A runtime assembly is required for AddComponent
// in native snap fixtures; the actual editor implementation remains unchanged.
public sealed class Piece : MonoBehaviour
{
    public void GetSnapPoints(List<Transform> points)
    {
        foreach (Transform child in transform)
            if (child.tag == "snappoint") points.Add(child);
    }
}

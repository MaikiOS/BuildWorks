using System;
using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class FogRenderProbe : MonoBehaviour
{
    public Action<RenderTexture, RenderTexture> Render;
    public int Frames;
    private void OnRenderImage(RenderTexture source, RenderTexture target)
    { if (Render != null) { ++Frames; Render(source, target); } else Graphics.Blit(source, target); }
}

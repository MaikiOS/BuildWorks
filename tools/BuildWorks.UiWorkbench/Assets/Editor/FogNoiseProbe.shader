Shader "Hidden/BuildWorks/FogNoiseProbe"
{
    Properties
    {
        _Noise ("Noise", 3D) = "gray" {}
        _FogOrigin ("Field origin", Vector) = (0,0,0,0)
        _NoiseOffset ("Building noise offset", Vector) = (0,0,0,0)
        _ProbeOrigin ("Probe origin", Vector) = (0,0,0,0)
        _Clock ("Clock", Float) = 0
        _Flow ("Flow", Float) = .12
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 4.0
            #include "UnityCG.cginc"
            #include "../BuildWorksFogRuntime/GhostMatter.cginc"
            float3 _ProbeOrigin;
            float4 frag(v2f_img i) : SV_Target
            {
                float3 p = _ProbeOrigin + float3(i.uv.x * 4, .5, i.uv.y * 4);
                return float4(GhostDrift(p), GhostNoise(p));
            }
            ENDCG
        }
    }
}

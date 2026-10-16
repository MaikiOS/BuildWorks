Shader "Hidden/BuildWorks/FogAtlasSamplingCheck"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 4.0
            #include "UnityCG.cginc"
            #include "Assets/BuildWorksFogRuntime/FogVolumeAtlas.cginc"
            sampler3D _Packed, _Original;
            float3 _PackedSize, _OriginalSize, _Offset;
            float _Slice;
            float4 frag(v2f_img i):SV_Target
            {
                float3 uv=float3(i.uv.x<.5?-.05:1.05,i.uv.y<.5?-.05:1.05,_Slice);
                float4 packed=tex3Dlod(_Packed,float4(FogAtlasUV(uv,_OriginalSize,_Offset,_PackedSize),0));
                float4 original=tex3Dlod(_Original,float4(uv,0));
                return abs(packed-original);
            }
            ENDCG
        }
    }
}

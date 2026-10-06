Shader "BuildWorks/ItemPreviewFixture"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) _Style ("Style", Int) = 0 }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            int _Style;
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            float4 frag() : SV_Target { return float4(_Color.rgb * (1 - _Style * 0.15), _Color.a); }
            ENDCG
        }
    }
}

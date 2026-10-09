Shader "BuildWorksTests/MaterialVariationProof"
{
    Properties { _Variation ("Variation", Vector) = (1,1,1,1) _Sentinel ("Caller", Float) = 37 }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ VARIATION_INSTANCED
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            #ifdef VARIATION_INSTANCED
                UNITY_INSTANCING_BUFFER_START(Variation)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Variation)
                UNITY_INSTANCING_BUFFER_END(Variation)
            #else
                float4 _Variation;
            #endif
            float _Sentinel;
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                return output;
            }
            float4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #ifdef VARIATION_INSTANCED
                    float4 value = UNITY_ACCESS_INSTANCED_PROP(Variation, _Variation);
                #else
                    float4 value = _Variation;
                #endif
                return float4(value.rgb * (_Sentinel / 37), 1);
            }
            ENDCG
        }
    }
}

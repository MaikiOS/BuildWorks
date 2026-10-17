Shader "BuildWorks/Fog/TexturedGhost"
{
    Properties
    {
        _MainTex ("Original albedo", 2D) = "white" {}
        _Color ("Original tint", Color) = (1,1,1,1)
        _BumpMap ("Original normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _Cutoff ("Original cutout", Range(0,1)) = 0
        _Noise ("Cloud noise", 3D) = "gray" {}
        _NoiseOffset ("Building noise offset", Vector) = (0,0,0,0)
        _MatterFlow ("Shared matter transport", 3D) = "black" {}
        _DenseContact ("Subdivided contact mesh", Float) = 0
        _ContactGeometry ("Transport textured geometry", Float) = 1
        _Opacity ("Ghost opacity", Range(0,1)) = .76
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        Cull Back ZWrite Off
        CGPROGRAM
        #pragma target 4.0
        #pragma surface surf Standard alpha:fade keepalpha fullforwardshadows vertex:vert
        #include "UnityStandardUtils.cginc"
        #include "GhostMatter.cginc"
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color;
        float _BumpScale, _Cutoff, _Opacity;
        float _DenseContact, _ContactGeometry;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float3 worldPos; float3 matterPosition; float3 restPosition; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 p = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.restPosition = p;
            float3 drift = GhostDrift(p);
            if (_DenseContact > .5)
            {
                // Interpolate the original triangle's drift so tessellation cannot change the approved idle shape.
                drift = GhostDrift(mul(unity_ObjectToWorld, float4(v.texcoord1.xyz,1)).xyz) * v.color.r
                    + GhostDrift(mul(unity_ObjectToWorld, float4(v.texcoord2.xyz,1)).xyz) * v.color.g
                    + GhostDrift(mul(unity_ObjectToWorld, float4(v.texcoord3.xyz,1)).xyz) * v.color.b;
            }
            o.matterPosition = p + drift;
            if (_DenseContact > .5) drift += GhostTransport(p).xyz * _ContactGeometry;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, drift);
        }
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            float3 p = _DenseContact > .5 ? i.matterPosition : i.worldPos;
            float3 drift = GhostDrift(p);
            // Geometry, texture and depletion share the undeformed source coordinates.
            float4 transport = GhostTransport(_Accumulated > .5 ? i.restPosition : p);
            float3 curl = transport.xyz;
            float2 curlUV = float2((curl.x + curl.z) * .5, curl.y) * .8;
            float2 flowingUV = i.uv_MainTex + drift.xz * .035 + curlUV;
            fixed4 textureColor = tex2D(_MainTex, flowingUV) * _Color;
            if (transport.w > .001)
            {
                float2 smear = curlUV * .4;
                fixed4 soft = (tex2D(_MainTex, flowingUV + smear) + tex2D(_MainTex, flowingUV - smear)
                    + tex2D(_MainTex, flowingUV + smear * .5) + tex2D(_MainTex, flowingUV - smear * .5)) * .25 * _Color;
                textureColor = lerp(textureColor, soft, transport.w * .85);
            }
            float originalAlpha = tex2D(_MainTex, i.uv_MainTex + drift.xz * .035).a * _Color.a;
            clip(originalAlpha - max(.001, _Cutoff));
            float noise = GhostNoise(p);
            float remaining = (1 - GhostClear(p)) * (1 - transport.w * .85);
            float dissolve = smoothstep(.12, .80, remaining);
            if (_Accumulated > .5)
            {
                remaining = 1 - transport.w;
                dissolve = smoothstep(.02 + noise * .06, .94 + noise * .06, remaining);
            }
            float gray = dot(textureColor.rgb, float3(.2126, .7152, .0722));
            o.Albedo = lerp(textureColor.rgb, gray.xxx, .04) * float3(.98, 1, 1.02);
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, i.uv_BumpMap + drift.xz * .035 + curlUV), _BumpScale);
            o.Normal = normalize(lerp(o.Normal, float3(0,0,1), transport.w * .75));
            o.Metallic = 0;
            o.Smoothness = 0;
            o.Emission = textureColor.rgb * (.6 + (1 - dissolve) * .15);
            float wisps = .28 + .72 * smoothstep(.18, .72, noise);
            o.Alpha = originalAlpha * _Opacity * dissolve * wisps;
        }
        ENDCG
    }
    Fallback Off
}

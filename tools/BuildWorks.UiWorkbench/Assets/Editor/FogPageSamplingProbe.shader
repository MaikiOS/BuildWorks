Shader "Hidden/BuildWorks/PageSamplingProbe"
{
 SubShader { Pass { ZTest Always ZWrite Off Cull Off
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 4.5
 #define BUILDWORKS_FOG_PUBLISHED 1
 #include "UnityCG.cginc"
 #include "../BuildWorksFogRuntime/FogPublished.cginc"
 int _ProbeField;
 float3 _ProbeUV;
 float4 frag(v2f_img input) : SV_Target { return FogPackedSample(0,(uint)_ProbeField,_ProbeUV); }
 ENDCG
 } }
}

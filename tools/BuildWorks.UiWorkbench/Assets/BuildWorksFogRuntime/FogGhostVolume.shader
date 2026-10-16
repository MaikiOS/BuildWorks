Shader "Hidden/BuildWorks/FogGhostVolumeRuntime"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} _Shape ("Building density", 3D) = "black" {} _Albedo ("Original building colour", 3D) = "gray" {} _Noise ("Cloud noise", 3D) = "gray" {} _Wake ("Transported matter density", 3D) = "black" {} _MatterFlow ("Shared matter transport", 3D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 4.0
            #pragma multi_compile_local __ BUILDWORKS_FOG_TWO_SOURCES BUILDWORKS_FOG_ATLAS
            #include "UnityCG.cginc"
            #include "GhostMatter.cginc"
            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler3D _Shape, _Wake, _Albedo;
            float3 _WakeMin, _WakeMax;
            float _WakeEnabled;
            float3 _VolumeMin, _VolumeMax, _CameraRight, _CameraUp, _CameraForward;
            float _Aspect, _TanHalfFov, _Density;
            float3 _FogCameraOrigin;
            float3 _BeamOrigin, _BeamDirection;
            float _BeamStrength, _BeamAttenuation;

            float3 densities(float3 p, out float3 pigment)
            {
                pigment = float3(.57, .60, .62);
                float3 uv = (p - _VolumeMin) / (_VolumeMax - _VolumeMin);
                float building = 0;
                if (all(uv >= 0) && all(uv <= 1))
                {
                    float shape = tex3Dlod(_Shape, float4(uv, 0)).r;
                    if (shape >= .002)
                    {
                        uv = (p - GhostDrift(p + _FogOrigin) - _VolumeMin) / (_VolumeMax - _VolumeMin);
                        shape = tex3Dlod(_Shape, float4(saturate(uv), 0)).r;
                        float remaining = _Accumulated > .5 ? 1-GhostTransport(p + _FogOrigin).w : (1-GhostClear(p + _FogOrigin))*(1-GhostTransport(p + _FogOrigin).w*.85);
                        building = shape * (.45 + GhostNoise(p + _FogOrigin) * .55) * remaining * _Density;
                    }
                }
                float wake = 0, wakeLight = 1;
                if (_WakeEnabled > .5)
                {
                    float3 wakeUV = (p - _WakeMin) / (_WakeMax - _WakeMin);
                    if (all(wakeUV >= 0) && all(wakeUV <= 1))
                    {
                        float4 smoke = tex3Dlod(_Wake, float4(wakeUV, 0));
                        float4 shade = tex3Dlod(_Wake, float4(wakeUV - _BeamDirection * .04, 0));
                        wake = (_Accumulated > .5 ? smoke.a : smoke.r) * _Density;
                        wakeLight = exp(_Accumulated > .5 ? -shade.a*.28 : -shade.r*.7);
                        float3 sourceUV = (p - GhostTransport(p + _FogOrigin).xyz - _VolumeMin) / (_VolumeMax - _VolumeMin);
                        pigment = lerp(pigment, tex3Dlod(_Albedo, float4(saturate(sourceUV), 0)).rgb, .45);
                        if (_Accumulated > .5)
                        {
                            pigment = lerp(smoke.rgb/max(.000001,smoke.a),float3(.70,.76,.82),.94);
                            float3 edge = min(p-_WakeMin,_WakeMax-p);
                            float3 delta = p-_Trail[0].xyz;
                            delta.y -= clamp(delta.y,-max(0,_Body.y-_Body.x),max(0,_Body.y-_Body.x));
                            wake *= .72*smoothstep(0,.24,min(edge.x,min(edge.y,edge.z)))
                                *smoothstep(_Body.x-.02,_Body.x+.06,length(delta));
                        }
                    }
                }
                return float3(building, wake, wakeLight);
            }
            float beam(float3 p)
            {
                float3 delta = p - _BeamOrigin;
                float along = dot(delta, _BeamDirection);
                if (along < 0 || along > 17) return 0;
                float3 up = normalize(cross(_BeamDirection, float3(0, 0, 1)));
                float a = dot(delta, up), b = delta.z;
                float width = .055 + along * .014;
                float result = 0;
                [unroll] for (int lane = -1; lane <= 1; ++lane)
                {
                    float offset = b - lane * .38;
                    result += exp(-(a * a + offset * offset) / (width * width));
                }
                return result * (1 - smoothstep(14, 17, along));
            }
            float lightTransmission(float3 p)
            {
                if (_BeamAttenuation < .5) return 1;
                float3 path = p - _BeamOrigin;
                float stepLength = length(path) / 24;
                float opticalDepth = 0;
                [loop] for (int s = 0; s < 24; ++s)
                {
                    float3 pigment;
                    float3 d = densities(_BeamOrigin+path*((s+.5)/24),pigment);
                    opticalDepth += (d.x*.9+d.y*(_Accumulated > .5 ? .4 : .9))*stepLength;
                }
                return exp(-opticalDepth);
            }
            #include "FogVolumePair.cginc"
            #include "FogVolumeAtlas.cginc"
            fixed4 frag(v2f_img i) : SV_Target
            {
#if defined(BUILDWORKS_FOG_ATLAS)
                return FogAtlasFragment(i);
#endif
#if defined(BUILDWORKS_FOG_TWO_SOURCES)
                return FogPairFragment(i);
#endif
                float4 background = tex2D(_MainTex, i.uv);
                float2 ndc = i.uv * 2 - 1;
                float3 ray = normalize(_CameraForward + _CameraRight * ndc.x * _Aspect * _TanHalfFov + _CameraUp * ndc.y * _TanHalfFov);
                float3 origin = _FogCameraOrigin - _FogOrigin;
                float3 safeRay = lerp(-1, 1, step(0, ray)) * max(abs(ray), .00001);
                float3 t0 = (_VolumeMin - float3(3, 0, 1) - origin) / safeRay;
                float3 t1 = (_VolumeMax + float3(4, 1, 1) - origin) / safeRay;
                float3 lo = min(t0, t1), hi = max(t0, t1);
                float enter = max(max(lo.x, lo.y), lo.z);
                float leave = min(min(hi.x, hi.y), hi.z);
                float eye = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                leave = min(leave, eye / max(.001, dot(ray, _CameraForward)));
                enter = max(enter, 0);
                if (leave <= enter) return background;
                float stepLength = (leave - enter) / 160;
                // Keep building/light quadrature fixed when the simulation padding changes.
                float3 fineMin = _Accumulated > .5 ? _VolumeMin : _WakeMin;
                float3 fineMax = _Accumulated > .5 ? _VolumeMax : _WakeMax;
                float3 wake0 = (fineMin - origin) / safeRay, wake1 = (fineMax - origin) / safeRay;
                float3 wakeLo = min(wake0, wake1), wakeHi = max(wake0, wake1);
                float wakeEnter = max(max(wakeLo.x, wakeLo.y), wakeLo.z);
                float wakeLeave = min(min(wakeHi.x, wakeHi.y), wakeHi.z);
                float jitter = frac(sin(dot(i.uv, float2(12.9898, 78.233))) * 43758.5453);
                float distance = enter + stepLength * jitter;
                float transmission = 1;
                float3 accumulated = 0;
                [loop] for (int k = 0; k < 512 && distance < leave; ++k)
                {
                    float ds = (_WakeEnabled > .5 || _Accumulated > .5) && distance >= wakeEnter && distance <= wakeLeave ? min(stepLength, .025) : stepLength;
                    float3 p = origin + ray * distance;
                    float3 pigment;
                    float3 matter = densities(p, pigment);
                    float d = matter.x + matter.y;
                    float mask = beam(p) * _BeamStrength;
                    float visibleLight = 0;
                    if (mask > .005)
                    {
                        // Integrate the same moving density between source and sample.
                        visibleLight = mask * lightTransmission(p);
                    }
                    float3 scatter = float3(.23, .12, .055) * matter.x * .11;
                    scatter += pigment * matter.y * (matter.z * .45 + .55) * (_Accumulated > .5 ? .5 : .85);
                    scatter += float3(1, .73, .39) * visibleLight * (.32 + d * .22);
                    accumulated += transmission * scatter * ds;
                    // Surface alpha already supplies most of the view extinction.
                    transmission *= exp(-(matter.x * .22 + matter.y * (_Accumulated > .5 ? .4 : .8) + .002) * ds);
                    if (transmission < .015) break;
                    distance += ds;
                }
                return float4(accumulated + transmission * background.rgb, 1);
            }
            ENDCG
        }
    }
}

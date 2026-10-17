#if defined(BUILDWORKS_FOG_TWO_SOURCES)
sampler3D _ShapeB, _AlbedoB, _NoiseB, _WakeB, _MatterFlowB;
float3 _VolumeMinB, _VolumeMaxB, _WakeMinB, _WakeMaxB, _MatterMinB, _MatterMaxB, _FogOriginB;
float4 _ActorB;
float2 _BodyB;
float _ClockB, _DensityB, _FlowB;
float3 _NoiseOffsetB;

float4 PairTransportB(float3 local)
{
    float3 uv = (local - _MatterMinB) / (_MatterMaxB - _MatterMinB);
    return any(uv < 0) || any(uv > 1) ? 0 : tex3Dlod(_MatterFlowB, float4(uv, 0));
}
float PairNoiseB(float3 local)
{
    local += _NoiseOffsetB;
    float3 drift = float3(.010, .016, -.008) * _ClockB;
    return tex3Dlod(_NoiseB, float4(local * .11 + drift, 0)).r * .72
        + tex3Dlod(_NoiseB, float4(local * .29 - drift, 0)).r * .28;
}
float3 PairDriftB(float3 local)
{
    return float3(PairNoiseB(local), PairNoiseB(local + 11.3), PairNoiseB(local - 7.9)) * 2 * _FlowB - _FlowB;
}
float3 PairDensityB(float3 world, out float3 pigment)
{
    float3 p = world - _FogOriginB;
    pigment = float3(.57, .60, .62);
    float3 uv = (p - _VolumeMinB) / (_VolumeMaxB - _VolumeMinB);
    float building = 0;
    if (all(uv >= 0) && all(uv <= 1))
    {
        float shape = tex3Dlod(_ShapeB, float4(uv, 0)).r;
        if (shape >= .002)
        {
            uv = (p - PairDriftB(p) - _VolumeMinB) / (_VolumeMaxB - _VolumeMinB);
            shape = tex3Dlod(_ShapeB, float4(saturate(uv), 0)).r;
            building = shape * (.45 + PairNoiseB(p) * .55) * (1 - PairTransportB(p).w) * _DensityB;
        }
    }
    float wake = 0, wakeLight = 1;
    float3 wakeUV = (p - _WakeMinB) / (_WakeMaxB - _WakeMinB);
    if (all(wakeUV >= 0) && all(wakeUV <= 1))
    {
        float4 smoke = tex3Dlod(_WakeB, float4(wakeUV, 0));
        float4 shade = tex3Dlod(_WakeB, float4(wakeUV - _BeamDirection * .04, 0));
        wake = smoke.a * _DensityB; wakeLight = exp(-shade.a * .28);
        pigment = lerp(smoke.rgb / max(.000001, smoke.a), float3(.70, .76, .82), .94);
        float3 edge = min(p - _WakeMinB, _WakeMaxB - p), delta = p - _ActorB.xyz;
        delta.y -= clamp(delta.y, -max(0, _BodyB.y - _BodyB.x), max(0, _BodyB.y - _BodyB.x));
        wake *= .72 * smoothstep(0, .24, min(edge.x, min(edge.y, edge.z)))
            * smoothstep(_BodyB.x - .02, _BodyB.x + .06, length(delta));
    }
    return float3(building, wake, wakeLight);
}

void PairMatter(float3 world, out float extinction, out float density, out float3 scatter)
{
    float3 pigmentA, pigmentB;
    float3 a = densities(world - _FogOrigin, pigmentA), b = PairDensityB(world, pigmentB);
    float building = a.x + b.x, wake = a.y + b.y;
    density = building + wake; extinction = building * .22 + wake * .4;
    scatter = float3(.23, .12, .055) * building * .11
        + pigmentA * a.y * (a.z * .45 + .55) * .5 + pigmentB * b.y * (b.z * .45 + .55) * .5;
}
float PairLightTransmission(float3 world)
{
    float3 start = _BeamOrigin + _FogOrigin, path = world - start;
    float stepLength = length(path) / 24, opticalDepth = 0;
    [loop] for (int s = 0; s < 24; ++s)
    {
        float3 pigmentA, pigmentB, p = start + path * ((s + .5) / 24);
        float3 a = densities(p - _FogOrigin, pigmentA), b = PairDensityB(p, pigmentB);
        opticalDepth += ((a.x + b.x) * .9 + (a.y + b.y) * .4) * stepLength;
    }
    return exp(-opticalDepth);
}
float4 FogPairFragment(v2f_img i)
{
    float4 background = tex2D(_MainTex, i.uv);
    float2 ndc = i.uv * 2 - 1;
    float3 ray = normalize(_CameraForward + _CameraRight * ndc.x * _Aspect * _TanHalfFov + _CameraUp * ndc.y * _TanHalfFov);
    float3 origin = _FogCameraOrigin, safeRay = lerp(-1, 1, step(0, ray)) * max(abs(ray), .00001);
    float3 minimum = min(_VolumeMin + _FogOrigin, _VolumeMinB + _FogOriginB) - float3(3, 0, 1);
    float3 maximum = max(_VolumeMax + _FogOrigin, _VolumeMaxB + _FogOriginB) + float3(4, 1, 1);
    float3 t0 = (minimum - origin) / safeRay, t1 = (maximum - origin) / safeRay;
    float3 lo = min(t0, t1), hi = max(t0, t1);
    float enter = max(max(lo.x, lo.y), lo.z), leave = min(min(hi.x, hi.y), hi.z);
    float eye = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
    leave = min(leave, eye / max(.001, dot(ray, _CameraForward))); enter = max(enter, 0);
    if (leave <= enter) return background;
    float stepLength = (leave - enter) / 160;
    float3 fineMin = min(_VolumeMin + _FogOrigin, _VolumeMinB + _FogOriginB);
    float3 fineMax = max(_VolumeMax + _FogOrigin, _VolumeMaxB + _FogOriginB);
    float3 fine0 = (fineMin - origin) / safeRay, fine1 = (fineMax - origin) / safeRay;
    float3 fineLo = min(fine0, fine1), fineHi = max(fine0, fine1);
    float fineEnter = max(max(fineLo.x, fineLo.y), fineLo.z), fineLeave = min(min(fineHi.x, fineHi.y), fineHi.z);
    float distance = enter + stepLength * frac(sin(dot(i.uv, float2(12.9898, 78.233))) * 43758.5453);
    float transmission = 1; float3 accumulated = 0;
    [loop] for (int k = 0; k < 768 && distance < leave; ++k)
    {
        float ds = distance >= fineEnter && distance <= fineLeave ? min(stepLength, .025) : stepLength;
        float3 world = origin + ray * distance, scatter; float extinction, density;
        PairMatter(world, extinction, density, scatter);
        float mask = beam(world - _FogOrigin) * _BeamStrength;
        float visibleLight = 0;
        [branch] if (mask > .005) visibleLight = mask * PairLightTransmission(world);
        scatter += float3(1, .73, .39) * visibleLight * (.32 + density * .22);
        accumulated += transmission * scatter * ds;
        transmission *= exp(-(extinction + .002) * ds);
        if (transmission < .015) break;
        distance += ds;
    }
    return float4(accumulated + transmission * background.rgb, 1);
}
#endif

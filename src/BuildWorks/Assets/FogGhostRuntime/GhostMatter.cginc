#ifndef BUILDWORKS_GHOST_MATTER
#define BUILDWORKS_GHOST_MATTER
#include "FogPublished.cginc"
sampler3D _Noise;
float4 _Trail[24];
float _Clock, _Disperse, _Flow;
float2 _Body;
sampler3D _MatterFlow;
float3 _MatterMin, _MatterMax;
float _MatterEnabled;
float _Accumulated;
float3 _FogOrigin;
float3 _NoiseOffset;

float GhostClock()
{
    #if defined(BUILDWORKS_FOG_PUBLISHED)
    FogPublishedState state = FogReadPublished(_PublishedRegion);
    return state.dormant ? _Clock : state.clock;
    #else
    return _Clock;
    #endif
}

float4 GhostTransport(float3 p)
{
    p -= _FogOrigin;
    #if defined(BUILDWORKS_FOG_PUBLISHED)
    FogPublishedState state = FogReadPublished(_PublishedRegion);
    if (!state.valid || !state.active) return 0;
    #else
    if (_MatterEnabled < .5) return 0;
    #endif
    float3 uv = (p - _MatterMin) / (_MatterMax - _MatterMin);
    if (any(uv < 0) || any(uv > 1)) return 0;
    #if defined(BUILDWORKS_FOG_PUBLISHED)
    float4 field = tex3Dlod(_PublishedSurface, float4(FogDynamicUV(uv, _DynamicFieldSize, _DynamicOffset), 0));
    #else
    float4 field = tex3Dlod(_MatterFlow, float4(uv, 0));
    #endif
    return field;
}

float GhostNoise(float3 p)
{
    p = p - _FogOrigin + _NoiseOffset;
    float3 drift = float3(.010, .016, -.008) * GhostClock();
    return tex3Dlod(_Noise, float4(p * .11 + drift, 0)).r * .72
        + tex3Dlod(_Noise, float4(p * .29 - drift, 0)).r * .28;
}
float GhostClear(float3 p)
{
    p -= _FogOrigin;
    float noise = tex3Dlod(_Noise, float4((p + _NoiseOffset) * .73 + GhostClock() * .025, 0)).r;
    float clear = 0;
    #if defined(BUILDWORKS_FOG_PUBLISHED)
    FogPublishedState state = FogReadPublished(_PublishedRegion);
    if (!state.valid && !state.dormant) return 0;
    float2 body = state.dormant ? _Body : float2(state.actorRadius.w, state.halfHeight);
    // The accumulated runtime keeps only trail[0]; older trail entries belong to the legacy variant.
    const int trailCount = 1;
    #else
    float2 body = _Body;
    const int trailCount = 24;
    #endif
    [unroll] for (int j = 0; j < trailCount; ++j)
    {
        #if defined(BUILDWORKS_FOG_PUBLISHED)
        float4 trail = state.dormant ? _Trail[0] : float4(state.actorRadius.xyz, state.weight);
        #else
        float4 trail = _Trail[j];
        #endif
        if (trail.w <= 0) continue;
        float3 delta = p - trail.xyz;
        delta.y -= clamp(delta.y, -max(0, body.y - body.x), max(0, body.y - body.x));
        float radius = length(delta) + (noise - .5) * .10;
        float weight = trail.w;
        float reach = (body.x + .20) * (.55 + .45 * weight);
        clear = max(clear, weight * weight * (1 - smoothstep(body.x * .85 * weight, reach, radius)));
    }
    return saturate(clear * _Disperse);
}
float3 GhostDrift(float3 p)
{
    return float3(GhostNoise(p), GhostNoise(p + 11.3), GhostNoise(p - 7.9)) * 2 * _Flow - _Flow;
}
#endif

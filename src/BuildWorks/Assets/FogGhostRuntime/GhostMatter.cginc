#ifndef BUILDWORKS_GHOST_MATTER
#define BUILDWORKS_GHOST_MATTER
sampler3D _Noise;
float4 _Trail[24];
float _Clock, _Disperse, _Flow;
float2 _Body;
sampler3D _MatterFlow;
float3 _MatterMin, _MatterMax;
float _MatterEnabled;
float _Accumulated;
float3 _FogOrigin;

float4 GhostTransport(float3 p)
{
    p -= _FogOrigin;
    if (_MatterEnabled < .5) return 0;
    float3 uv = (p - _MatterMin) / (_MatterMax - _MatterMin);
    if (any(uv < 0) || any(uv > 1)) return 0;
    float4 field = tex3Dlod(_MatterFlow, float4(uv, 0));
    return field;
}

float GhostNoise(float3 p)
{
    p -= _FogOrigin;
    float3 drift = float3(.010, .016, -.008) * _Clock;
    return tex3Dlod(_Noise, float4(p * .11 + drift, 0)).r * .72
        + tex3Dlod(_Noise, float4(p * .29 - drift, 0)).r * .28;
}
float GhostClear(float3 p)
{
    p -= _FogOrigin;
    float noise = tex3Dlod(_Noise, float4(p * .73 + _Clock * .025, 0)).r;
    float clear = 0;
    [unroll] for (int j = 0; j < 24; ++j)
    {
        if (_Trail[j].w <= 0) continue;
        float3 delta = p - _Trail[j].xyz;
        delta.y -= clamp(delta.y, -max(0, _Body.y - _Body.x), max(0, _Body.y - _Body.x));
        float radius = length(delta) + (noise - .5) * .10;
        float weight = _Trail[j].w;
        float reach = (_Body.x + .20) * (.55 + .45 * weight);
        clear = max(clear, weight * weight * (1 - smoothstep(_Body.x * .85 * weight, reach, radius)));
    }
    return saturate(clear * _Disperse);
}
float3 GhostDrift(float3 p)
{
    return float3(GhostNoise(p), GhostNoise(p + 11.3), GhostNoise(p - 7.9)) * 2 * _Flow - _Flow;
}
#endif

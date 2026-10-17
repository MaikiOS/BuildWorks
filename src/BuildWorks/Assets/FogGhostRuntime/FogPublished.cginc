#ifndef BUILDWORKS_FOG_PUBLISHED_INCLUDED
#define BUILDWORKS_FOG_PUBLISHED_INCLUDED
#if defined(BUILDWORKS_FOG_PUBLISHED)
// Immutable bindings: slot, owner low/high, generation low/high. Publication precedes every draw.
#if !defined(SHADER_TARGET_SURFACE_ANALYSIS)
StructuredBuffer<uint> _FogBindings, _PublishedMetadata;
#endif
int _PublishedSlots, _PublishedRegion;
sampler3D _PublishedSurface;
float3 _DynamicAtlasSize, _DynamicFieldSize, _DynamicOffset;
struct FogPublishedState
{
    float4 actorRadius;
    float halfHeight, clock, weight;
    bool valid, active, dormant;
};
FogPublishedState FogReadPublished(int region)
{
    FogPublishedState state = (FogPublishedState)0;
    // Unity's surface-analysis parser cannot read StructuredBuffer syntax; generated GPU passes can.
    #if !defined(SHADER_TARGET_SURFACE_ANALYSIS)
    uint bindingWords, bindingStride, metadataWords, metadataStride;
    _FogBindings.GetDimensions(bindingWords, bindingStride);
    _PublishedMetadata.GetDimensions(metadataWords, metadataStride);
    if (region < 0 || (uint)region >= bindingWords / 5 || bindingStride != 4 || metadataStride != 4 || _PublishedSlots <= 0) return state;
    if ((uint)_PublishedSlots > metadataWords / 16) return state;
    uint binding = region * 5, slot = _FogBindings[binding];
    // Only the explicit unassigned tuple may use this source's CPU clock/body; corrupt bindings stay inert.
    state.dormant = slot == 0xffffffffu && _FogBindings[binding + 1] == 0 && _FogBindings[binding + 2] == 0
        && _FogBindings[binding + 3] == 0 && _FogBindings[binding + 4] == 0;
    if (slot >= (uint)_PublishedSlots || slot >= metadataWords / 16) return state;
    uint base = slot * 16, flags = _PublishedMetadata[base + 7];
    if ((flags & 1) == 0 || _PublishedMetadata[base + 8] != _FogBindings[binding + 1]
        || _PublishedMetadata[base + 9] != _FogBindings[binding + 2]
        || _PublishedMetadata[base + 10] != _FogBindings[binding + 3]
        || _PublishedMetadata[base + 11] != _FogBindings[binding + 4]) return state;
    state.valid = true; state.active = (flags & 2) != 0;
    state.actorRadius = asfloat(uint4(_PublishedMetadata[base], _PublishedMetadata[base + 1], _PublishedMetadata[base + 2], _PublishedMetadata[base + 3]));
    state.halfHeight = asfloat(_PublishedMetadata[base + 4]);
    state.clock = asfloat(_PublishedMetadata[base + 5]); state.weight = asfloat(_PublishedMetadata[base + 6]);
    #endif
    return state;
}
float3 FogDynamicUV(float3 uv, float3 size, float3 offset)
{ return (offset + 1 + saturate(uv) * size) / _DynamicAtlasSize; }
#endif
#endif

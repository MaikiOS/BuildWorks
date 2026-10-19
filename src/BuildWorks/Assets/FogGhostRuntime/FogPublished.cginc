#ifndef BUILDWORKS_FOG_PUBLISHED_INCLUDED
#define BUILDWORKS_FOG_PUBLISHED_INCLUDED
#if defined(BUILDWORKS_FOG_PUBLISHED)
// Immutable bindings: slot, owner low/high, generation low/high. Publication precedes every draw.
#if !defined(SHADER_TARGET_SURFACE_ANALYSIS)
StructuredBuffer<uint> _FogBindings, _PublishedMetadata;
StructuredBuffer<uint> _PublishedMaps, _PublishedMapBases;

#endif
int _PublishedSlots, _PublishedRegion;
int _PublishedSparse, _PublishedBlockCapacity;
sampler3D _PublishedSurface, _PublishedBricks;
float3 _PublishedBrickGrid;
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
// A physical page includes a one-cell halo; the native sampler retains its filtering.
float4 FogPackedSample(int region, uint field, float3 uv, FogPublishedState state)
{
    #if !defined(SHADER_TARGET_SURFACE_ANALYSIS)
    if (!state.valid || !state.active || (field != 0 && field != 2)) return 0;
    uint bases, baseStride, maps, mapStride;
    _PublishedMapBases.GetDimensions(bases, baseStride); _PublishedMaps.GetDimensions(maps, mapStride);
    uint slot = _FogBindings[region * 5];
    if (slot >= bases || baseStride != 4 || mapStride != 4 || any(_PublishedBrickGrid < 1)) return 0;
    uint base = _PublishedMapBases[slot], first = slot * 57600;
    if ((base != first && base != first + 28800) || base > maps || maps - base < 28800) return 0;
    float3 q = saturate(uv) * float3(96,80,80) - .5;
    int3 cell = (int3)floor(q);
    uint capacity = (uint)(_PublishedBrickGrid.x * _PublishedBrickGrid.y * _PublishedBrickGrid.z);
    if (_PublishedBlockCapacity < 1 || (uint)_PublishedBlockCapacity > capacity) return 0;
    // Any occupied neighbour page contains all eight taps, including across an empty block.
    [unroll] for(int z=0;z<2;++z) [unroll] for(int y=0;y<2;++y) [unroll] for(int x=0;x<2;++x)
    {
        uint3 block = (uint3)clamp(cell+int3(x,y,z),int3(0,0,0),int3(95,79,79))/4;
        uint page = _PublishedMaps[base + field * 9600 + block.x + 24*(block.y+20*block.z)];
        if(page == 0xffffffffu) continue;
        if(page >= (uint)_PublishedBlockCapacity) return 0;
        float3 tile = float3(page % (uint)_PublishedBrickGrid.x, (page / (uint)_PublishedBrickGrid.x) % (uint)_PublishedBrickGrid.y,
            page / (uint)(_PublishedBrickGrid.x * _PublishedBrickGrid.y)) * 6;
        return tex3Dlod(_PublishedBricks,float4((tile+1.5+q-(float3)block*4)/(_PublishedBrickGrid*6),0));
    }
    #endif
    return 0;
}
float4 FogPackedSample(int region, uint field, float3 uv)
{ return FogPackedSample(region,field,uv,FogReadPublished(region)); }
#endif
#endif

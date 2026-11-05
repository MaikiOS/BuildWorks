float3 FogAtlasUV(float3 uv, float3 size, float3 offset, float3 atlasSize)
{ return (offset + 1 + saturate(uv) * size) / atlasSize; }
#if defined(BUILDWORKS_FOG_ATLAS)
sampler3D _AtlasShape, _AtlasSmoke, _AtlasSurface;
float3 _AtlasSize, _FineMin, _FineMax;
int _RegionCount, _IntegrationSteps;
float4 _RegionOriginClock[32], _RegionRestMin[32], _RegionRestMax[32], _RegionFieldMin[32], _RegionFieldMax[32];
float4 _RegionActorRadius[32], _RegionShapeSize[32], _RegionFieldSize[32], _RegionOffset[32];
float4 _RegionNoiseOffset[32];
#if defined(BUILDWORKS_FOG_PUBLISHED)
float4 _RegionDynamicOffset[32];
#endif

float3 AtlasUV(float3 uv, float3 size, int owner)
{ return FogAtlasUV(uv,size,_RegionOffset[owner].xyz,_AtlasSize); }
float AtlasNoise(float3 local, int owner)
{
    local += _RegionNoiseOffset[owner].xyz;
    #if defined(BUILDWORKS_FOG_PUBLISHED)
    FogPublishedState state = FogReadPublished(owner);
    float clock = state.dormant ? _RegionOriginClock[owner].w : state.clock;
    #else
    float clock = _RegionOriginClock[owner].w;
    #endif
    float3 drift = float3(.010,.016,-.008) * clock;
    return tex3Dlod(_Noise,float4(local*.11+drift,0)).r*.72 + tex3Dlod(_Noise,float4(local*.29-drift,0)).r*.28;
}
float3 AtlasDrift(float3 local, int owner)
{ return (float3(AtlasNoise(local,owner),AtlasNoise(local+11.3,owner),AtlasNoise(local-7.9,owner))*2-1)*.12; }
float AtlasNoiseAtClock(float3 local, int owner, float clock)
{
    local += _RegionNoiseOffset[owner].xyz;
    float3 drift = float3(.010,.016,-.008) * clock;
    return tex3Dlod(_Noise,float4(local*.11+drift,0)).r*.72 + tex3Dlod(_Noise,float4(local*.29-drift,0)).r*.28;
}
float3 AtlasDriftAtClock(float3 local, int owner, float clock)
{ return (float3(AtlasNoiseAtClock(local,owner,clock),AtlasNoiseAtClock(local+11.3,owner,clock),AtlasNoiseAtClock(local-7.9,owner,clock))*2-1)*.12; }
float3 AtlasDensities(float3 p, int owner, out float3 pigment)
{
    p -= _RegionOriginClock[owner].xyz;
    pigment = float3(.57,.60,.62);
    float3 uv = (p-_RegionRestMin[owner].xyz)/(_RegionRestMax[owner].xyz-_RegionRestMin[owner].xyz);
    float3 wakeUV = (p-_RegionFieldMin[owner].xyz)/(_RegionFieldMax[owner].xyz-_RegionFieldMin[owner].xyz);
    bool inRest = all(uv>=0) && all(uv<=1), inField = all(wakeUV>=0) && all(wakeUV<=1);
    bool active = true;
    #if defined(BUILDWORKS_FOG_PUBLISHED)
    #if defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
    FogPublishedState state = FogReadPublished(owner);
    #else
    // Leave density arithmetic unchanged; outside both volumes no publication data is used.
    FogPublishedState state = (FogPublishedState)0;
    [branch] if(inRest || inField) state = FogReadPublished(owner);
    #endif
    active = state.valid && state.active;
    #if !defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
    active = active && !state.dynamicEmpty;
    #endif
    inField = inField && active;
    float clock = state.dormant ? _RegionOriginClock[owner].w : state.clock;
    #else
    #if defined(BUILDWORKS_FOG_DORMANT_FAST)
    active = _RegionShapeSize[owner].w > .5;
    inField = inField && active;
    #endif
    #endif
    float building=0, wake=0, wakeLight=1;
    #if !defined(BUILDWORKS_FOG_WAKE_ONLY) || !defined(BUILDWORKS_FOG_PUBLISHED) || defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
    [branch] if (inRest)
    {
        float shape = tex3Dlod(_AtlasShape,float4(AtlasUV(uv,_RegionShapeSize[owner].xyz,owner),0)).r;
        [branch] if (shape>=.002)
        {
            #if defined(BUILDWORKS_FOG_PUBLISHED) && !defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
            float3 drift=AtlasDriftAtClock(p,owner,clock);
            #else
            float3 drift=AtlasDrift(p,owner);
            #endif
            float3 moved=(p-drift-_RegionRestMin[owner].xyz)/(_RegionRestMax[owner].xyz-_RegionRestMin[owner].xyz);
            shape=tex3Dlod(_AtlasShape,float4(AtlasUV(moved,_RegionShapeSize[owner].xyz,owner),0)).r;
            float damage=0;
            #if defined(BUILDWORKS_FOG_PUBLISHED)
            #if defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
            [branch] if(active) damage=(_PublishedSparse != 0 ? FogPackedSample(owner,2,wakeUV) : tex3Dlod(_AtlasSurface,float4(FogDynamicUV(wakeUV,_RegionFieldSize[owner].xyz,_RegionDynamicOffset[owner].xyz),0))).w;
            #else
            [branch] if(active)
            {
                [branch] if(_PublishedSparse != 0) damage=FogPackedSample(owner,2,wakeUV,state).w;
                else damage=tex3Dlod(_AtlasSurface,float4(FogDynamicUV(wakeUV,_RegionFieldSize[owner].xyz,_RegionDynamicOffset[owner].xyz),0)).w;
            }
            #endif
            #else
            [branch] if(active) damage=tex3Dlod(_AtlasSurface,float4(AtlasUV(wakeUV,_RegionFieldSize[owner].xyz,owner),0)).w;
            #endif
            #if defined(BUILDWORKS_FOG_PUBLISHED) && !defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
            float noise=AtlasNoiseAtClock(p,owner,clock);
            #else
            float noise=AtlasNoise(p,owner);
            #endif
            building=shape*(.45+noise*.55)*(1-damage)*.85;
        }
    }
    #endif
    [branch] if(inField)
    {
        #if defined(BUILDWORKS_FOG_PUBLISHED)
        #if defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
        float4 smoke=_PublishedSparse != 0 ? FogPackedSample(owner,0,wakeUV) : tex3Dlod(_AtlasSmoke,float4(FogDynamicUV(wakeUV,_RegionFieldSize[owner].xyz,_RegionDynamicOffset[owner].xyz),0));
        float4 shade=_PublishedSparse != 0 ? FogPackedSample(owner,0,wakeUV-_BeamDirection*.04) : tex3Dlod(_AtlasSmoke,float4(FogDynamicUV(wakeUV-_BeamDirection*.04,_RegionFieldSize[owner].xyz,_RegionDynamicOffset[owner].xyz),0));
        #else
        float4 smoke,shade;
        [branch] if(_PublishedSparse != 0)
        {
            smoke=FogPackedSample(owner,0,wakeUV,state);
            shade=FogPackedSample(owner,0,wakeUV-_BeamDirection*.04,state);
        }
        else
        {
            smoke=tex3Dlod(_AtlasSmoke,float4(FogDynamicUV(wakeUV,_RegionFieldSize[owner].xyz,_RegionDynamicOffset[owner].xyz),0));
            shade=tex3Dlod(_AtlasSmoke,float4(FogDynamicUV(wakeUV-_BeamDirection*.04,_RegionFieldSize[owner].xyz,_RegionDynamicOffset[owner].xyz),0));
        }
        #endif
        #else
        float4 smoke=tex3Dlod(_AtlasSmoke,float4(AtlasUV(wakeUV,_RegionFieldSize[owner].xyz,owner),0));
        float4 shade=tex3Dlod(_AtlasSmoke,float4(AtlasUV(wakeUV-_BeamDirection*.04,_RegionFieldSize[owner].xyz,owner),0));
        #endif
        wake=smoke.a*.85; wakeLight=exp(-shade.a*.28);
        pigment=lerp(smoke.rgb/max(.000001,smoke.a),float3(.70,.76,.82),.94);
        #if defined(BUILDWORKS_FOG_PUBLISHED)
        float4 actorRadius=state.actorRadius;
        float halfHeight=state.halfHeight;
        #else
        float4 actorRadius=_RegionActorRadius[owner];
        float halfHeight=_RegionFieldSize[owner].w;
        #endif
        float3 edge=min(p-_RegionFieldMin[owner].xyz,_RegionFieldMax[owner].xyz-p), delta=p-actorRadius.xyz;
        float radius=actorRadius.w;
        delta.y-=clamp(delta.y,-max(0,halfHeight-radius),max(0,halfHeight-radius));
        wake*=.72*smoothstep(0,.24,min(edge.x,min(edge.y,edge.z)))*smoothstep(radius-.02,radius+.06,length(delta));
    }
    return float3(building,wake,wakeLight);
}
#if defined(BUILDWORKS_FOG_PUBLISHED) && !defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
#define BUILDWORKS_FOG_CANDIDATE_MASK 1
#endif
void AtlasCandidates(float3 origin, float3 ray, float enter, float leave,
    #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
    out uint candidates)
    #else
    out int owners[32], out int count)
    #endif
{
    #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
    candidates=0;
    #else
    count=0;
    [unroll] for(int clear=0;clear<32;++clear) owners[clear]=0;
    #endif
    float3 safeRay=lerp(-1,1,step(0,ray))*max(abs(ray),.00001);
    [loop] for(int owner=0;owner<_RegionCount;++owner)
    {
        float3 minimum=_RegionFieldMin[owner].xyz+_RegionOriginClock[owner].xyz;
        float3 maximum=_RegionFieldMax[owner].xyz+_RegionOriginClock[owner].xyz;
        #if defined(BUILDWORKS_FOG_DORMANT_FAST)
        #if defined(BUILDWORKS_FOG_PUBLISHED)
        FogPublishedState state=FogReadPublished(owner);
        bool dormant=!state.valid || !state.active;
        #if !defined(BUILDWORKS_FOG_BOUNDS_BASELINE)
        dormant = dormant || state.dynamicEmpty;
        #endif
        #else
        bool dormant=_RegionShapeSize[owner].w<.5;
        #endif
        [branch] if(dormant)
        {
            // Dormant fields contain no transported matter; retain a conservative parallel-ray margin.
            float margin=.00001*(1+max(abs(enter),abs(leave)));
            minimum=_RegionRestMin[owner].xyz+_RegionOriginClock[owner].xyz-margin;
            maximum=_RegionRestMax[owner].xyz+_RegionOriginClock[owner].xyz+margin;
        }
        #endif
        float3 t0=(minimum-origin)/safeRay,t1=(maximum-origin)/safeRay,lo=min(t0,t1),hi=max(t0,t1);
        if(max(enter,max(max(lo.x,lo.y),lo.z))<min(leave,min(min(hi.x,hi.y),hi.z)))
        {
            #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
            candidates |= 1u << owner;
            #else
            owners[count++]=owner;
            #endif
        }
    }
}
void AtlasMatter(float3 p,
    #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
    uint candidates,
    #else
    int owners[32], int count,
    #endif
    out float extinction, out float density, out float3 scatter)
{
    extinction=0; density=0; scatter=0;
    #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
    uint remaining=candidates;
    [loop] while(remaining!=0)
    #else
    [loop] for(int i=0;i<count;++i)
    #endif
    {
        #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
        int owner=(int)FogTakeCandidate(remaining);
        #else
        int owner=owners[i];
        #endif
        float3 pigment=float3(.57,.60,.62);
        float3 matter=AtlasDensities(p,owner,pigment);
        density+=matter.x+matter.y; extinction+=matter.x*.22+matter.y*.4;
        scatter+=float3(.23,.12,.055)*matter.x*.11+pigment*matter.y*(matter.z*.45+.55)*.5;
    }
}
float AtlasLight(float3 p)
{
    float3 path=p-_BeamOrigin; float lengthPath=length(path);
    #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
    uint candidates; AtlasCandidates(_BeamOrigin,path/max(.00001,lengthPath),0,lengthPath,candidates);
    #else
    int owners[32],count; AtlasCandidates(_BeamOrigin,path/max(.00001,lengthPath),0,lengthPath,owners,count);
    #endif
    float opticalDepth=0,ds=lengthPath/24;
    [loop] for(int s=0;s<24;++s)
    {
        float3 sample=_BeamOrigin+path*((s+.5)/24);
        #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
        uint remaining=candidates;
        [loop] while(remaining!=0)
        #else
        [loop] for(int i=0;i<count;++i)
        #endif
        {
            #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
            int owner=(int)FogTakeCandidate(remaining);
            #else
            int owner=owners[i];
            #endif
            float3 pigment=float3(.57,.60,.62);
            float3 matter=AtlasDensities(sample,owner,pigment);
            opticalDepth+=(matter.x*.9+matter.y*.4)*ds;
        }
    }
    return exp(-opticalDepth);
}
#if defined(BUILDWORKS_FOG_WAKE_ONLY) && defined(BUILDWORKS_FOG_CANDIDATE_MASK)
// Experimental contact volume; the textured surface remains the building's visible form.
float4 IntegrateWake(float2 uv)
{
    float2 ndc=uv*2-1;
    float3 ray=normalize(_CameraForward+_CameraRight*ndc.x*_Aspect*_TanHalfFov+_CameraUp*ndc.y*_TanHalfFov);
    float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv))/max(.001,dot(ray,_CameraForward));
    float enter=depth,leave=0;
    uint candidates=0;
    [loop] for(int owner=0;owner<_RegionCount;++owner)
    {
        FogPublishedState state=FogReadPublished(owner);
        [branch] if(!state.valid || !state.active || state.dynamicEmpty) continue;
        float3 minimum=_RegionFieldMin[owner].xyz+_RegionOriginClock[owner].xyz;
        float3 maximum=_RegionFieldMax[owner].xyz+_RegionOriginClock[owner].xyz;
        float near=0,far=depth;
        // Parallel axes must neither divide by zero nor invent an intersection outside a field.
        [unroll] for(int axis=0;axis<3;++axis)
        {
            if(abs(ray[axis])<.0000001)
            {
                if(_FogCameraOrigin[axis]<minimum[axis] || _FogCameraOrigin[axis]>maximum[axis]) far=-1;
            }
            else
            {
                float a=(minimum[axis]-_FogCameraOrigin[axis])/ray[axis];
                float b=(maximum[axis]-_FogCameraOrigin[axis])/ray[axis];
                near=max(near,min(a,b)); far=min(far,max(a,b));
            }
        }
        if(far<=near) continue;
        candidates|=1u<<owner; enter=min(enter,near); leave=max(leave,far);
    }
    if(candidates==0) return float4(0,0,0,1);
    float distance=enter+.025*frac(sin(dot(uv,float2(12.9898,78.233)))*43758.5453);
    float transmission=1; float3 accumulated=0;
    [loop] for(int k=0;k<4096 && k<_IntegrationSteps && distance<leave;++k)
    {
        float ds=min(.025,leave-distance),extinction,density; float3 scatter;
        AtlasMatter(_FogCameraOrigin+ray*distance,candidates,extinction,density,scatter);
        accumulated+=transmission*scatter*ds;
        transmission*=exp(-extinction*ds);
        if(transmission<.015) break;
        distance+=ds;
    }
    return float4(accumulated,transmission);
}
float4 FogWakeOnlyFragment(v2f_img i)
{
    float4 background=tex2D(_MainTex,i.uv),wake=IntegrateWake(i.uv);
    return float4(wake.rgb+wake.a*background.rgb,background.a);
}
#endif
float4 FogAtlasFragment(v2f_img i)
{
    #if defined(BUILDWORKS_FOG_WAKE_ONLY) && defined(BUILDWORKS_FOG_CANDIDATE_MASK)
    return FogWakeOnlyFragment(i);
    #else
    float4 background=tex2D(_MainTex,i.uv);
    float2 ndc=i.uv*2-1;
    float3 ray=normalize(_CameraForward+_CameraRight*ndc.x*_Aspect*_TanHalfFov+_CameraUp*ndc.y*_TanHalfFov);
    float3 origin=_FogCameraOrigin,safeRay=lerp(-1,1,step(0,ray))*max(abs(ray),.00001);
    float3 t0=(_FineMin-float3(3,0,1)-origin)/safeRay,t1=(_FineMax+float3(4,1,1)-origin)/safeRay;
    float3 lo=min(t0,t1),hi=max(t0,t1);
    float enter=max(0,max(max(lo.x,lo.y),lo.z)),leave=min(min(hi.x,hi.y),hi.z);
    float eye=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv));
    leave=min(leave,eye/max(.001,dot(ray,_CameraForward)));
    if(leave<=enter) return background;
    #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
    uint candidates; AtlasCandidates(origin,ray,enter,leave,candidates);
    #else
    int owners[32],count; AtlasCandidates(origin,ray,enter,leave,owners,count);
    #endif
    float3 fine0=(_FineMin-origin)/safeRay,fine1=(_FineMax-origin)/safeRay,fineLo=min(fine0,fine1),fineHi=max(fine0,fine1);
    float fineEnter=max(max(fineLo.x,fineLo.y),fineLo.z),fineLeave=min(min(fineHi.x,fineHi.y),fineHi.z);
    float stepLength=(leave-enter)/160;
    float distance=enter+stepLength*frac(sin(dot(i.uv,float2(12.9898,78.233)))*43758.5453);
    float transmission=1; float3 accumulated=0;
    [loop] for(int k=0;k<4096 && k<_IntegrationSteps && distance<leave;++k)
    {
        float ds=distance>=fineEnter && distance<=fineLeave?min(stepLength,.025):stepLength;
        float3 p=origin+ray*distance,scatter;float extinction,density;
        #if defined(BUILDWORKS_FOG_CANDIDATE_MASK)
        AtlasMatter(p,candidates,extinction,density,scatter);
        #else
        AtlasMatter(p,owners,count,extinction,density,scatter);
        #endif
        float mask=beam(p)*_BeamStrength,visibleLight=0;
        [branch] if(mask>.005) visibleLight=mask*AtlasLight(p);
        scatter+=float3(1,.73,.39)*visibleLight*(.32+density*.22);
        accumulated+=transmission*scatter*ds;
        transmission*=exp(-(extinction+.002)*ds);
        if(transmission<.015) break;
        distance+=ds;
    }
    return float4(accumulated+transmission*background.rgb,1);
    #endif
}
#endif

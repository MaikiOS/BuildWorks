float3 FogAtlasUV(float3 uv, float3 size, float3 offset, float3 atlasSize)
{ return (offset + 1 + saturate(uv) * size) / atlasSize; }
#if defined(BUILDWORKS_FOG_ATLAS)
sampler3D _AtlasShape, _AtlasSmoke, _AtlasSurface;
float3 _AtlasSize, _FineMin, _FineMax;
int _RegionCount, _IntegrationSteps;
float4 _RegionOriginClock[32], _RegionRestMin[32], _RegionRestMax[32], _RegionFieldMin[32], _RegionFieldMax[32];
float4 _RegionActorRadius[32], _RegionShapeSize[32], _RegionFieldSize[32], _RegionOffset[32];

float3 AtlasUV(float3 uv, float3 size, int owner)
{ return FogAtlasUV(uv,size,_RegionOffset[owner].xyz,_AtlasSize); }
float AtlasNoise(float3 local, int owner)
{
    float3 drift = float3(.010,.016,-.008) * _RegionOriginClock[owner].w;
    return tex3Dlod(_Noise,float4(local*.11+drift,0)).r*.72 + tex3Dlod(_Noise,float4(local*.29-drift,0)).r*.28;
}
float3 AtlasDrift(float3 local, int owner)
{ return (float3(AtlasNoise(local,owner),AtlasNoise(local+11.3,owner),AtlasNoise(local-7.9,owner))*2-1)*.12; }
float3 AtlasDensities(float3 p, int owner, out float3 pigment)
{
    p -= _RegionOriginClock[owner].xyz;
    pigment = float3(.57,.60,.62);
    float3 uv = (p-_RegionRestMin[owner].xyz)/(_RegionRestMax[owner].xyz-_RegionRestMin[owner].xyz);
    float3 wakeUV = (p-_RegionFieldMin[owner].xyz)/(_RegionFieldMax[owner].xyz-_RegionFieldMin[owner].xyz);
    bool inRest = all(uv>=0) && all(uv<=1), inField = all(wakeUV>=0) && all(wakeUV<=1);
    bool active = true;
    #if defined(BUILDWORKS_FOG_DORMANT_FAST)
    active = _RegionShapeSize[owner].w > .5;
    inField = inField && active;
    #endif
    float building=0, wake=0, wakeLight=1;
    [branch] if (inRest)
    {
        float shape = tex3Dlod(_AtlasShape,float4(AtlasUV(uv,_RegionShapeSize[owner].xyz,owner),0)).r;
        [branch] if (shape>=.002)
        {
            float3 moved=(p-AtlasDrift(p,owner)-_RegionRestMin[owner].xyz)/(_RegionRestMax[owner].xyz-_RegionRestMin[owner].xyz);
            shape=tex3Dlod(_AtlasShape,float4(AtlasUV(moved,_RegionShapeSize[owner].xyz,owner),0)).r;
            float damage=0;
            [branch] if(active) damage=tex3Dlod(_AtlasSurface,float4(AtlasUV(wakeUV,_RegionFieldSize[owner].xyz,owner),0)).w;
            building=shape*(.45+AtlasNoise(p,owner)*.55)*(1-damage)*.85;
        }
    }
    [branch] if(inField)
    {
        float4 smoke=tex3Dlod(_AtlasSmoke,float4(AtlasUV(wakeUV,_RegionFieldSize[owner].xyz,owner),0));
        float4 shade=tex3Dlod(_AtlasSmoke,float4(AtlasUV(wakeUV-_BeamDirection*.04,_RegionFieldSize[owner].xyz,owner),0));
        wake=smoke.a*.85; wakeLight=exp(-shade.a*.28);
        pigment=lerp(smoke.rgb/max(.000001,smoke.a),float3(.70,.76,.82),.94);
        float3 edge=min(p-_RegionFieldMin[owner].xyz,_RegionFieldMax[owner].xyz-p), delta=p-_RegionActorRadius[owner].xyz;
        float radius=_RegionActorRadius[owner].w, halfHeight=_RegionFieldSize[owner].w;
        delta.y-=clamp(delta.y,-max(0,halfHeight-radius),max(0,halfHeight-radius));
        wake*=.72*smoothstep(0,.24,min(edge.x,min(edge.y,edge.z)))*smoothstep(radius-.02,radius+.06,length(delta));
    }
    return float3(building,wake,wakeLight);
}
void AtlasCandidates(float3 origin, float3 ray, float enter, float leave, out int owners[32], out int count)
{
    count=0;
    [unroll] for(int clear=0;clear<32;++clear) owners[clear]=0;
    float3 safeRay=lerp(-1,1,step(0,ray))*max(abs(ray),.00001);
    [loop] for(int owner=0;owner<_RegionCount;++owner)
    {
        float3 minimum=_RegionFieldMin[owner].xyz+_RegionOriginClock[owner].xyz;
        float3 maximum=_RegionFieldMax[owner].xyz+_RegionOriginClock[owner].xyz;
        #if defined(BUILDWORKS_FOG_DORMANT_FAST)
        [branch] if(_RegionShapeSize[owner].w<.5)
        {
            // Dormant fields contain no transported matter; retain a conservative parallel-ray margin.
            float margin=.00001*(1+max(abs(enter),abs(leave)));
            minimum=_RegionRestMin[owner].xyz+_RegionOriginClock[owner].xyz-margin;
            maximum=_RegionRestMax[owner].xyz+_RegionOriginClock[owner].xyz+margin;
        }
        #endif
        float3 t0=(minimum-origin)/safeRay,t1=(maximum-origin)/safeRay,lo=min(t0,t1),hi=max(t0,t1);
        if(max(enter,max(max(lo.x,lo.y),lo.z))<min(leave,min(min(hi.x,hi.y),hi.z))) owners[count++]=owner;
    }
}
void AtlasMatter(float3 p, int owners[32], int count, out float extinction, out float density, out float3 scatter)
{
    extinction=0; density=0; scatter=0;
    [loop] for(int i=0;i<count;++i)
    {
        float3 pigment=float3(.57,.60,.62);
        float3 matter=AtlasDensities(p,owners[i],pigment);
        density+=matter.x+matter.y; extinction+=matter.x*.22+matter.y*.4;
        scatter+=float3(.23,.12,.055)*matter.x*.11+pigment*matter.y*(matter.z*.45+.55)*.5;
    }
}
float AtlasLight(float3 p)
{
    float3 path=p-_BeamOrigin; float lengthPath=length(path);
    int owners[32],count; AtlasCandidates(_BeamOrigin,path/max(.00001,lengthPath),0,lengthPath,owners,count);
    float opticalDepth=0,ds=lengthPath/24;
    [loop] for(int s=0;s<24;++s)
    {
        float3 sample=_BeamOrigin+path*((s+.5)/24);
        [loop] for(int i=0;i<count;++i)
        {
            float3 pigment=float3(.57,.60,.62);
            float3 matter=AtlasDensities(sample,owners[i],pigment);
            opticalDepth+=(matter.x*.9+matter.y*.4)*ds;
        }
    }
    return exp(-opticalDepth);
}
float4 FogAtlasFragment(v2f_img i)
{
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
    int owners[32],count; AtlasCandidates(origin,ray,enter,leave,owners,count);
    float3 fine0=(_FineMin-origin)/safeRay,fine1=(_FineMax-origin)/safeRay,fineLo=min(fine0,fine1),fineHi=max(fine0,fine1);
    float fineEnter=max(max(fineLo.x,fineLo.y),fineLo.z),fineLeave=min(min(fineHi.x,fineHi.y),fineHi.z);
    float stepLength=(leave-enter)/160;
    float distance=enter+stepLength*frac(sin(dot(i.uv,float2(12.9898,78.233)))*43758.5453);
    float transmission=1; float3 accumulated=0;
    [loop] for(int k=0;k<4096 && k<_IntegrationSteps && distance<leave;++k)
    {
        float ds=distance>=fineEnter && distance<=fineLeave?min(stepLength,.025):stepLength;
        float3 p=origin+ray*distance,scatter;float extinction,density;
        AtlasMatter(p,owners,count,extinction,density,scatter);
        float mask=beam(p)*_BeamStrength,visibleLight=0;
        [branch] if(mask>.005) visibleLight=mask*AtlasLight(p);
        scatter+=float3(1,.73,.39)*visibleLight*(.32+density*.22);
        accumulated+=transmission*scatter*ds;
        transmission*=exp(-(extinction+.002)*ds);
        if(transmission<.015) break;
        distance+=ds;
    }
    return float4(accumulated+transmission*background.rgb,1);
}
#endif

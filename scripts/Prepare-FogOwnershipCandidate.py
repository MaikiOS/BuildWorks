from pathlib import Path
import json,hashlib
root=Path(__file__).resolve().parents[1];out=root/'artifacts/fog-return-ownership-222';out.mkdir(parents=True,exist_ok=True)
source=root/'src/BuildWorks/Assets/FogGhostRuntime/AccumulatedSmoke.compute'
text=source.read_text(encoding='utf-8-sig')
receipt=json.loads((root/'artifacts/fog-runtime-assets-candidate/verification.json').read_text(encoding='utf-8-sig'))
sha=hashlib.sha256(source.read_bytes()).hexdigest().upper()
assert next(s['sha256'] for s in receipt['sources'] if s['name']==source.name)==sha
replacements={
'uint2 SourceBin(float3 p) { return min((uint2)(saturate(RestUV(p).yz)*LedgerSize),LedgerSize-1); }':
'uint3 SourceBin(float3 p) { return min((uint3)(saturate(RestUV(p))*uint3(16,32,32)),uint3(15,31,31)); }',
'uint2 bin = SourceBin(source);':'uint3 bin = SourceBin(source);',
'uint2 bin = SourceBin(p);':'uint3 bin = SourceBin(p);',
'// ponytail: source YZ bins fit this one wall; use per-piece 3D ownership before a whole building.':'// Experimental 3D ownership only; runtime pool/layout/budgets remain unchanged.',
'_AwayMass[bin.x+LedgerSize*bin.y]':'_AwayMass[bin.x+16*(bin.y+32*bin.z)]',
'[unroll] for(int y=-1;y<=1;++y) [unroll] for(int x=-1;x<=1;++x)':
'[unroll] for(int z=-1;z<=1;++z) [unroll] for(int y=-1;y<=1;++y) [unroll] for(int x=-1;x<=1;++x)',
'uint2 neighbour = (uint2)clamp((int2)bin+int2(x,y),0,(int)LedgerSize-1);':
'uint3 neighbour = (uint3)clamp((int3)bin+int3(x,y,z),int3(0,0,0),int3(15,31,31));',
'uint index=neighbour.x+LedgerSize*neighbour.y;':'uint index=neighbour.x+16*(neighbour.y+32*neighbour.z);'
}
for old,new in replacements.items():
    assert text.count(old)==1,old
    text=text.replace(old,new)
path=root/'tools/BuildWorks.UiWorkbench/Assets/Editor/OwnershipCandidate/SpatialLedger.compute'
path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,encoding='utf-8')
(out/'candidate-source.json').write_text(json.dumps({'runtimeSourceSha256':sha,'candidatePath':str(path),'candidateSha256':hashlib.sha256(path.read_bytes()).hexdigest().upper(),'productionSourceModified':False,'thresholdUnchanged':768,'layout':[16,32,32],'prototypeOnly':True},indent=2),encoding='utf-8')
print('Generated isolated candidate from pinned runtime source; production compute and bundle unchanged.')

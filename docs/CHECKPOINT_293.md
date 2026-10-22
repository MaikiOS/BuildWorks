# 293 — depth-sampling assumption reproduced

22October2026. No Valheim/world changes, unchanged292shader. DX11 GPU control uses full8×8depth, low4×4S/T and8×8output, filling **every** depth texel at its real center with an analytic reciprocal-depth plane. Inner pixel4,4 and source alpha checked.

The same plane292 rejects this ideal plane with **Point** filtering and accepts it with **Bilinear**. Compiler0, normal editor exit/settings restored. The former manually consistent four-tap fixture missed real sample-center offsets.

This proves a synthetic coverage gap, not the actual Valheim sampler or the complete native cost cause. Shader292 unchanged. Next required fact: actual native `_CameraDepthTexture` size/filter under the owned camera. No tolerance widening or further A/B before this fact. Existing RenderProbe.Capture ownership guard remains; no new capture branch.

Evidence: `artifacts/checkpoint-293/{sampling/verification.json,Composite.log,Composite-verification.json,Composite-exit.json}`. Native sampling, a coordinate correction and resulting speed remain unverified; v11 remains open.

Next observation prepared in existing FogCandidateProbe: at most4reads of global depth after its fixed owned camera in candidate mode, stopping at first non-null; name,size,filterMode,texelSize and frame recorded. No Camera.Render/readback/input/material writes. Helper build0/0; Astra review found no blockers. Reads restricted to final owned camera/candidate and stale errors cleared on success; rebuild0/0. Not yet native proof. Filter metadata alone does not guarantee the actual hardware sampler.

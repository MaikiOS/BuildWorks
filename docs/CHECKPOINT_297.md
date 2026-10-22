# 297 — coherent depth addressing passes local GPU gates

22October2026. Isolated candidate, **not installed or accepted as a v11 fix**. Production shader/include/bundle and installedQA267unchanged.

Both passes derive the same integer full-depth texel from the low pixel, sampling its center. Low integration accepts explicit depth while ray UV/jitter, S/T weights, plane tolerances, exact raw equality and legacy full fallback remain unchanged. A local include copy adds only overload/wrapper; S/T addresses use checked xy dimensions rather than duplicate reciprocal zw. No new RT/settings/controller.

The first GPU attempt passed assertions but failed the zero-message compiler gate: `integer divides may be much slower, try using uints if possible`. Unsigned division preserves the formula within guarded bounds. Original failure retained; gate not weakened.

- **75 GPU assertions**,0compiler messages:8×8/9×9/3440×1440 analytic planes, Point/Bilinear, original S/T weights/source alpha, sky/thin/nonplanar/clamped/invalid controls. Native-sized textures do not mean Valheim execution.
- Actual8×8low integration uses published GPU buffers and smoke texture: nonempty fog, shared selected depth clips it, thin full-pixel foreground invokes exact legacy fallback. Synthetic state/depth is not the complete host scene.
- Linear/DX11 bundle loaded twice, exactly2shaders,0messages; SHA256 `B652698B12B40899745AF8550636D7691222BCF225557F9BC6A3830FCEBB6D7F`.
- Four-section wall/four directions/20frames/48source comparisons, Gamma960×540. Empty/recovered exact, max mean byte delta0.01831/255, max channel9/255. Inspected largest-mean-delta pair shows no obvious displacement. These selected diagnostic4-owner frames do not prove full animation/v11 acceptance.
- Helper build0/0, Python7/7; independent Astra correctness/Ponytail PASS for one transient native ABBA. Negative source-Y untested. SocratiCode green60552/watcheractive; narrow experimental shader search had no hits, so current files were directly inspected without reindexing.

## Native measurement not executed

Prepared85-step297/289/289/297 contact ABBA on existing unpaid house3440×1440, no screenshots/grants/construction/terrain. Candidate activates before Ready/weather waits. Both games_start attempts failed at GABS state publication: `failed to publish runtime state: rename .runtime-*.tmp runtime.json: Access is denied`, including the elevated client. Repetitions stopped.

Status plus actual CIM confirmed no Valheim; final claim remains starting/preflight without game PID. Both owned clients closed,3bridge DLLs restored,97project and250config baseline hashes unchanged. No claim/preparation/token reset. Host inventory/FPS/scenario remain **unverified**. An overlay test of actual GABS Claim/Save reproduced Windows errno5 while os.Open held a reader: baseline and after-close saves passed. Go opens without FILE_SHARE_DELETE. This confirms the conflict mechanism, not the actual holder during those starts. No matching Defender1123events. This blocks the specific measurement, not the goal; do not replace the existing controller blindly.

Next: fix the existing GABS reader/atomic replacement conflict while retaining ownership checks, then execute the prepared single ABBA. Reject without useful acceleration or if edges fail. No new owner acceptance ready. Evidence: `artifacts/checkpoint-297/`, `artifacts/fog-longwall-explicit-297-probe/`, `ValheimQA/artifacts/20261022-090740/`. Compiler failure/initial71/final75 evidence retained. Windows wildcard os error123 and PowerShell empty-pipe parser errors were corrected without counting failed commands as verification. verify skill unavailable; actual checks listed above. FS-only overlay changed no GABS source/live state/ACL. Initial offline dependency-cache miss was resolved using the already pinned golang.org/x/text v0.22.0 via the official Go proxy; actual filesystem test PASS, raw errno recorded in fs-only/result.json.

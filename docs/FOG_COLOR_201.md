<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Fog v11 color contract: .201

2026-10-16. The pale .200 wall was reproduced with a Gamma-built shader bundle in a Linear/HDR/Deferred host. The bundle build now explicitly targets Linear; all four runtime shader/compute sources and the approved v11 archive remain unchanged.

Build-FogGhostAssets.ps1 verifies the effective color space. -GammaReference writes a separate artifact without replacing the embedded product bundle. C# and PowerShell restore Workbench settings; ProjectSettings.asset was byte-exact after checks. LoadEmbedded rejects Gamma hosts; the explicit resource constructor still supports reference comparisons.

The product bundle contains the same three owned assets, 49,715 bytes, SHA `0DD52DF3790D17756DA5A5F9D45B54F23FAD0716E8B7FB95A7662B4E2BAAF889`, with no game models or textures.

## Verified

- Unity6000.0.61f1 Linear/HDR/Deferred surface-at-rest comparison: Linear bundle versus direct source mean RGB delta0; wrong Gamma bundle delta0.048705826878076335. Embedded loading in Gamma was rejected. This does not establish full Linear volume parity or user visual acceptance.
- Separate approved Gamma-lab comparison: six frames/two translations within the existing threshold; Shape/Pigment/Noise byte-exact. Product Linear simulation:270 ticks/18 complete Smoke/Flow/Surface exact comparisons plus fractional/catch-up/invalid/disposal checks passed.
- Normal/QA builds0 errors/0 warnings; Host Steam25909357, QA-control/isolation, localization1104 passed. Verify helper unavailable; direct checks and independent correctness PASSED/Ponytail Ship completed.
- QA0.19.201.0 installed only in TerrainRamp-1.0-Test, DLL SHA `F8829697C625613FB2D84E917A66D4FED8D29EAE1DDE7518C40498C4DA6C3022`;143 protected files retained SHA/UTCmtime at deployment.
- Native Valheim launched through Thunderstore Modded with only the existing isolated QA saves available. Inspected idle/contact screenshots: wood color/transparency retained. Actual player motion: damage0→1→1 standing→0 after exit; contact smoke2.98046875. No Failure; temporary root and component reference removed.
- Final visible14 inventory entries, quantities/slots/displayed durability unchanged, Wood9; not a full native inventory-byte comparison. Legacy WAL v4 SHA `95A4C7430457CDF2130246421F144F607F2B4E4ED0F17894C447E553BB602CF8` unchanged. Protections restored; game closed normally; owned helper stopped; endpoint absent.

Evidence stays in artifacts/fog-material-201, fog-color-check, fog-runtime-assets{,-gamma}, fog-runtime-visual and fog-runtime-simulation. Native screenshots were copied before bridge pruning and are not published. Initial captured-input JSON parsing failed on the trailing Screenshot line; splitting it yielded completed reports, while native contact/recovery were independently asserted. Sky refresh found no screenshot target after Exit; process absence was separately verified.

## Open

One wall only: user appearance acceptance, full Linear volume parity, rotated/intersecting source ownership and performance remain open. Existing fields cost approximately46.9MiB per region; do not allocate one region per piece. Next multiple sources with bounded memory, then persistent unfinished project/stake/board/material continuation/cancel-refund/reload/server. Fog is not integrated into ordinary construction yet. Full goal ACTIVE.

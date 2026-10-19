# Pinned legacy v11 Linear bundle

Frozen on 2026-10-19 from `src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11`
before promoting the Spatial3D/window candidate. BuildWorks-owned shader asset;
not a Valheim game asset. Size: 62061 bytes.

SHA256: `DD9576EC6925082A5B59615C7EB813CC8B93067FFCBAB5D926AACE29558C61DD`.

`FogSpatialLedgerCheck.LegacyFixtureBytes` verifies these exact bytes before use
in the legacy rejection and published legacy-pixel comparisons. Build scripts must
never overwrite this fixture. It is distinct from the `.223` spatial baseline and
from the current runtime candidate. Freezing it is not visual acceptance or runtime
promotion of that candidate.

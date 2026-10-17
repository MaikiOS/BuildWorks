# Installing and verifying BuildWorks

[Русский](INSTALL_RU.md) · [Current source/QA/public versions](CURRENT_STATUS.md)

Public installation: use an explicitly published package from [GitHub Releases](https://github.com/MaikiOS/BuildWorks/releases), with Valheim and BepInExPack Valheim5.4.x. Latest public ZIP is0.19.37 alpha and is historical; do not expect .206 source features in it. Compatibility must be checked for the chosen package/host; .206 host proof does not certify .37 on a newer game.

Close the game/server and back up your own world/character before a public installation. Keep the matching pair together:

```text
BepInEx/plugins/Ostrix-BuildWorks/BuildWorks.dll
BepInEx/plugins/Ostrix-BuildWorks/BuildWorks.Geometry.dll
```

Never leave a second older DLL elsewhere in plugins. Startup `BepInEx/LogOutput.log` must show `BuildWorks <installed-version> loaded.`; verify the actual version, not this example. UI absence needs the full log, host version and mod list.

## Developer QA on Ostrix's workstation

Only `TerrainRamp-1.0-Test` is authorized. `Default/BuildWorks` is the source tree; `Default/BepInEx/plugins` must not contain BuildWorks. Use `scripts/Deploy-TestBuild.ps1 -TargetProfile TerrainRamp-1.0-Test` first with `-WhatIf`. The existing isolated stand uses `-QaStandToken <existing-token>` after building with `-p:BuildWorksTestStand=true`; never create/reset a token or saves to repopulate cards. The script verifies hashes and refuses running Valheim and other profiles. Deployment does not itself authorize game/world actions.

Launch through Thunderstore → TerrainRamp-1.0-Test → **Modded**, then only the authorized existing QA world/character. No direct Steam/game launch. QA .206 is installed; it has not run in Valheim. Do not change a DLL while any Valheim process is running.

## Data and removal

Normal library: `BepInEx/config/BuildWorks/blueprints.json`; managed portable files: the same BuildWorks directory under `share/<id>.buildworks.json`. QA redirects these into the existing isolated save root. Use the in-game Share path instead of guessing a profile. The share folder is not automatically imported or server-published.

Close the game before removing only `BepInEx/plugins/Ostrix-BuildWorks`. Back up blueprints/share files first; plugin removal does not undo already built world objects. Never delete worlds/characters or another profile to uninstall.

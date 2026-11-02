# Install private alpha 0.19.329

[Русский](INSTALL_RU.md)

BuildWorks 0.19.329 — PRIVATE TEST CANDIDATE
Private alpha for manual testing on a second PC or a collaborator profile.

Use a working Valheim profile with BepInEx 5. The verified host is Steam build 26249040. Valheim and BepInEx are not included.

1. Close Valheim completely.
2. Open the profile folder that contains BepInEx. With a mod manager, use the selected profile folder; with a manual installation, use the game's existing BepInEx folder.
3. Back up BepInEx/plugins/Ostrix-BuildWorks OUTSIDE every plugins folder. Back up existing BuildWorks configuration and blueprint data before upgrading.
4. Extract the archive's BepInEx folder into that profile folder and merge. Replace all four supplied files in BepInEx/plugins/Ostrix-BuildWorks together: BuildWorks.dll, BuildWorks.Geometry.dll, YamlDotNet.dll and THIRD_PARTY_NOTICES.md.
5. Keep one active BuildWorks installation. Keep your existing configuration, blueprint library, worlds and characters. Do not copy a QA profile, bridge, test-only DLLs or isolation tokens.
6. Start that modded profile. Verify that BepInEx/LogOutput.log loads BuildWorks 0.19.329 once. For the second PC, repeat these steps in its own profile.

This is a manual-install private ZIP, not a Thunderstore package or a public beta. No files are installed automatically.

To remove or roll back: close Valheim and remove only Ostrix-BuildWorks, or restore its backup. Leave player data in place. Older binaries may require the matching pre-upgrade blueprint-data backup.

See QA_SCOPE_EN.txt for the accepted tests and their limits. Source/build/isolated gameplay do not establish compatibility with every mod stack or multiplayer. Please return a fresh LogOutput.log, the exact game/mod version and the blueprint that reproduces any placement issue.


[Full version/evidence limits](ALPHA_0.19.329.md).

## Data and removal

Normal library: `BepInEx/config/BuildWorks/blueprints.json`; managed portable files: the same BuildWorks directory under `share/<id>.buildworks.json`. QA redirects these into the existing isolated save root. Use the in-game Share path instead of guessing a profile. The share folder is not automatically imported or server-published.

Close the game before removing only `BepInEx/plugins/Ostrix-BuildWorks`. Back up blueprints/share files first; plugin removal does not undo already built world objects. Never delete worlds/characters or another profile to uninstall.

Verify the supplied SHA256SUMS.txt: a version number alone does not identify earlier329 test iterations. Use only the owner-issued private archive; no public download is claimed.

# Install alpha .329

[Русский](INSTALL_RU.md)

[Download / Скачать](https://github.com/MaikiOS/BuildWorks/releases/tag/v0.19.329-alpha)

The archive is not rebuilt; internal private labels describe its original issue. Use existing Valheim with BepInEx 5. Verified host: Steam build 26249040 / QA BepInEx5.4.23.6 / Unity6000.0.75f1. Other host versions are not established by this evidence.

1. Close Valheim completely.
2. Open the profile folder that contains BepInEx. With a mod manager, use the selected profile folder; with a manual installation, use the game's existing BepInEx folder.
3. Back up BepInEx/plugins/Ostrix-BuildWorks OUTSIDE every plugins folder. Back up existing BuildWorks configuration and blueprint data before upgrading.
4. Extract the archive's BepInEx folder into that profile folder and merge. Replace all four supplied files in BepInEx/plugins/Ostrix-BuildWorks together: BuildWorks.dll, BuildWorks.Geometry.dll, YamlDotNet.dll and THIRD_PARTY_NOTICES.md.
5. Keep one active BuildWorks installation. Keep your existing configuration, blueprint library, worlds and characters. Do not copy a QA profile, bridge, test-only DLLs or isolation tokens.
6. Start that modded profile. Verify that BepInEx/LogOutput.log loads BuildWorks 0.19.329 once. For the second PC, repeat these steps in its own profile.

ZIP SHA256: `3C91113CB4D601A843317DA251BB60BF191107EC5D7E15D74983279373934E41`.

## Updating, data and removal

Close the game before updating. Keep backups outside plugins. Replace all four supplied files together; do not mix DLL builds. Valheim/BepInEx are not included. To uninstall, remove only BepInEx/plugins/Ostrix-BuildWorks; existing world pieces/worlds/characters are not rolled back. Preserve configuration and BepInEx/config/BuildWorks, including blueprints.json and share. Binary rollback may require the matching library backup. Do not copy a QA profile, bridge or tokens.

[Version / Ограничения](ALPHA_0.19.329.md).

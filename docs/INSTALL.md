# Installing BuildWorks 0.19.33 alpha

**English** | [Русский](INSTALL_RU.md)

## Before installation

1. Close Valheim and any Valheim server.
2. Back up the world and character.
3. Install BepInExPack Valheim 5.4.x.

## Manual installation

Extract the archive so the files are located as follows:

```text
Valheim/
└─ BepInEx/
   └─ plugins/
      └─ Ostrix-BuildWorks/
         ├─ BuildWorks.dll
         └─ BuildWorks.Geometry.dll
```

Do not install the Geometry DLL separately and do not leave an older `BuildWorks.dll` beside it.

## Verifying startup

After launch, `BepInEx/LogOutput.log` should contain:

```text
BuildWorks 0.19.33 loaded.
```

If the UI does not appear, attach the complete `LogOutput.log`, the Valheim version, and the installed mod list to your report.

## Uninstalling

Close the game and remove only this directory:

```text
BepInEx/plugins/Ostrix-BuildWorks/
```

Do not remove saved blueprints until you have backed them up separately.

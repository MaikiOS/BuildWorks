# BuildWorks

**English** | [Русский](README_RU.md)

> Source candidate 0.19.47 for Valheim 1.0.16. The latest existing binary prerelease remains 0.19.37; this checkpoint does not publish a new binary. Development tests use only TerrainRamp-1.0-Test; thematic points, Auto attachment and native resource cards await owner game acceptance.

![BuildWorks concept cover](docs/images/buildworks-hero-concept.png)

BuildWorks is a precision construction toolkit for Valheim. It extends the regular hammer with an indexed catalog, lets players assemble composite blueprints in a dedicated editor, and provides precise placement for individual pieces or an entire blueprint in the world.

The central rule is simple: players still build real Valheim pieces. BuildWorks does not replace a structure with one decorative model and does not alter terrain behind the scenes.

```mermaid
flowchart LR
    A[Vanilla hammer] --> B[BuildWorks index]
    B --> C[Single piece]
    B --> D[Blueprint library]
    D --> E[Blueprint Editor]
    E --> F[Pieces and groups]
    E --> G[Array and Contour]
    E --> H[Blueprint anchor]
    C --> I[F9 precision placement]
    D --> I
    I --> J[Native Valheim validation]
    J --> K[Real pieces in the world]
```

## What works now

- indexed hammer UI with categories, materials, recents, favorites, search, and blueprints;
- creating a blueprint from world pieces or from an empty document;
- a dedicated editor with a catalog, camera, grid, object tree, and Undo/Redo;
- precise translation, three-axis rotation, and uniform scaling;
- persistent editor Q/E snap-mode feedback and visible nearby target points within 2 m, without changing the 0.55 m magnetic snap threshold;
- groups, nesting, local group anchors, and a separate world anchor for the blueprint;
- Array with line/plane layouts, Pack/Fit/exact spacing, rise, heading, pitch, roll, scale step, and symmetry;
- Contour repetition of a selected piece or group along a connected chain;
- F9 world editing for a piece or composite blueprint;
- sequential construction of real pieces using normal Valheim resources, permissions, and restrictions.
- English-first runtime localization with a complete Russian catalog.

Full documentation: [features and interactions](docs/FEATURES.md), [controls](docs/CONTROLS.md), [architecture](docs/ARCHITECTURE.md), [localization](docs/LOCALIZATION.md), [installation](docs/INSTALL.md), and [roadmap](docs/ROADMAP.md).

## Alpha status

Candidate 0.19.41 fixes the contour guide to full edge endpoints, fully hides editor occluders with F7/button, organizes shortcuts into six groups, adds editor-only FOV controls with game reset, and compacts the object tree with anchor badges, eye/lock columns and an overflow scrollbar. Release, geometry/store/localization, editor bridge, world layout, host/deployment checks and 81 fresh Unity Workbench captures plus input regressions pass. A focused game series and multiplayer remain owner gates.

See [the ordered development roadmap](docs/ROADMAP.md) and [current acceptance series](specs/roadmap/current-pass.md). [0.19.37](docs/ALPHA_0.19.37.md) remains historical release evidence.

## License and pull requests

BuildWorks is proprietary/source-available software, not open-source software. The official binaries may be installed for personal, non-commercial testing. You may inspect and fork the source solely to prepare a pull request. Reusing BuildWorks code or original materials in another project, redistributing them, or using them commercially requires prior written permission from the owner.

See [LICENSE](LICENSE) for the governing terms, the [Russian convenience translation](LICENSE_RU.md), and [CONTRIBUTING.md](CONTRIBUTING.md) to propose a change.

## Dependencies

- Valheim;
- BepInExPack Valheim 5.4.x.

Jotunn, TerrainRamp, and EarthWorks are not BuildWorks dependencies.

## Building from source

You need the .NET SDK, an installed copy of Valheim, and BepInExPack. BuildWorks compiles directly against Valheim host assemblies, so provide local paths without committing game DLLs to the repository:

```powershell
dotnet build .\src\BuildWorks\BuildWorks.csproj -c Release `
  -p:ProfileRoot="C:\path\to\BepInEx-profile" `
  -p:ValheimManagedDir="C:\path\to\Valheim\valheim_Data\Managed"
```

Deterministic geometry checks:

```powershell
dotnet run --project .\tests\BuildWorks.GeometryTests\BuildWorks.GeometryTests.csproj -c Release
```

## Important limitation

This is an early testing alpha, not a stable public release. Back up the world and character before use. Multiplayer, the complete save/exit/reload matrix, blueprint import/export, and future procedural curves have not passed their final gates.

The concept cover above was created for this repository and is not an in-game screenshot. The remaining documentation images are captures of the implemented UI from the Unity Workbench. See [IMAGES.md](docs/IMAGES.md) for provenance.

# Contributing to BuildWorks

**English** | [Русский](CONTRIBUTING_RU.md)

BuildWorks is proprietary/source-available software. Pull requests are welcome, but repository access is not permission to reuse the code or assets in another project.

## Before editing

1. Read [the architecture map](docs/ARCHITECTURE.md) and the [current alpha status](docs/ALPHA_0.19.37.md).
2. Search every caller before changing a shared host adapter or helper.
3. Keep stable IDs, serialized field names, localization keys, and translated labels separate.
4. Preserve native Valheim placement, resource, permission, ownership, and networking checks.
5. Keep one Undo entry per user operation and treat store migrations/atomic writes as data-safety code.

The lower-case fields in `CompositeBlueprintStore` are an existing JSON schema. Renaming them requires an explicit migration. Large orchestrator files should be split only when the extracted responsibility has independent ownership and a focused test.

## Player-facing text

English is the fallback and primary documentation language. Russian is a complete runtime/documentation duplicate. Add every player-facing key to both TSV catalogs and never compare translated text in logic. Follow [LOCALIZATION.md](docs/LOCALIZATION.md).

## Required automated checks

Run from the repository root. Report exactly which checks ran and which Valheim scenarios did not.

```powershell
dotnet build .\src\BuildWorks\BuildWorks.csproj -c Release
.\scripts\Test-Localization.ps1 -RequireBuiltAssembly
dotnet run --project .\tests\BuildWorks.LocalizationTests\BuildWorks.LocalizationTests.csproj -c Release
dotnet format .\src\BuildWorks\BuildWorks.csproj --verify-no-changes --severity warn --no-restore
dotnet run --project .\tests\BuildWorks.GeometryTests\BuildWorks.GeometryTests.csproj -c Release
dotnet run --project .\tests\BuildWorks.StoreTests\BuildWorks.StoreTests.csproj -c Release
.\tests\BuildWorks.EditorBridgeChecks\Run.ps1
.\tests\BuildWorks.WorldLayoutChecks\Run.ps1
.\scripts\Test-HostContract.ps1
.\scripts\Capture-UiWorkbench.ps1
```

The last command launches the isolated Unity Workbench, not Valheim. No automated check replaces an owner-run in-game smoke test, save/reload, or multiplayer acceptance.

## Pull requests

- Open an issue first and keep one behavioral concern per pull request.
- Do not add third-party code or assets without documented permission.
- Do not weaken or delete a gate to make a change pass.
- Include the reason, affected responsibility, tests, runtime gaps, and migration impact.

By submitting a contribution, you accept the `Contributions` section of [LICENSE](LICENSE).

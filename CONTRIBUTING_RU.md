# Как предложить изменение

[English](CONTRIBUTING.md) | **Русский**

BuildWorks — proprietary/source-available проект. Pull Request приветствуются, но доступ к репозиторию не разрешает использовать код или материалы в другом проекте.

## Перед правкой

1. Прочитайте [карту архитектуры](docs/ARCHITECTURE_RU.md) и [текущий статус альфы](docs/ALPHA_0.19.37_RU.md).
2. Перед изменением общего host-adapter или helper найдите всех его вызывающих.
3. Не смешивайте стабильные ID, поля сохранения, ключи локализации и переведённые подписи.
4. Сохраняйте native-проверки Valheim: размещение, ресурсы, права, владение и сеть.
5. Одна операция пользователя должна давать одну запись Undo; миграции и атомарная запись store относятся к сохранности данных.

Поля в нижнем регистре внутри `CompositeBlueprintStore` — существующая JSON-схема. Их переименование требует явной миграции. Большие orchestrator-файлы следует делить только при появлении самостоятельной ответственности с отдельным тестом.

## Текст для игрока

English — основной язык документации и обязательный fallback. Russian — полный runtime/documentation дубль. Каждый пользовательский ключ добавляется в оба TSV; переведённый текст нельзя сравнивать в логике. Подробности: [LOCALIZATION_RU.md](docs/LOCALIZATION_RU.md).

## Обязательные автоматические проверки

Запускайте из корня репозитория и указывайте, что реально проверено, а что не проверялось в Valheim.

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

Последняя команда запускает изолированный Unity Workbench, а не Valheim. Автоматические проверки не заменяют игровой smoke владельца, save/reload и multiplayer.

## Pull Request

- Сначала откройте Issue; один Pull Request — одна поведенческая задача.
- Не добавляйте чужой код или материалы без подтверждённого разрешения.
- Не ослабляйте проверки ради зелёного результата.
- Опишите причину, затронутую ответственность, тесты, непроверенные runtime-границы и влияние на миграцию.

Отправляя вклад, вы соглашаетесь с разделом `Contributions` файла [LICENSE](LICENSE).

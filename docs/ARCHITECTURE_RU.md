# Архитектура BuildWorks

[English](ARCHITECTURE.md) | **Русский**

У BuildWorks две сборки. `BuildWorks.dll` интегрируется с Valheim и Unity. `BuildWorks.Geometry.dll` содержит детерминированные расчёты и операции с документом без зависимости от движка. Объекты Valheim нельзя переносить в Geometry: её тесты должны запускаться без игры.

## Карта runtime-кода

| Ответственность | Начинать здесь | Связанные файлы |
|---|---|---|
| Жизненный цикл плагина, config, сборка Harmony-патчей | `BuildWorksPlugin.cs` | `BuildWorksLocalization.cs` |
| Индексный и ванильный Hammer | `UnifiedHammerCatalog.cs` | `HammerBlueprintPieceRegistry.cs` |
| Мировой F9 и последовательная установка | `PrecisionPlacementSession.cs` | `PrecisionPlacementHudView.cs`, `TransformGizmoView.cs`, `PlacementGhostPreviewView.cs` |
| Хранение чертежей и миграции формата | `CompositeBlueprintStore.cs` | `BlueprintThumbnailRenderer.cs` |
| Управление изолированным редактором | `BlueprintEditorController.cs` | `BlueprintEditorInput.cs` |
| UI редактора и дерево объектов | `BlueprintEditorView.cs` | `BlueprintEditorSkin.cs`, `BlueprintEditorIconLibrary.cs` |
| Камера, временные объекты, hit-test и snap | `BlueprintEditorScene.cs` | `BlueprintEditorMeshData.cs` |
| Документ, Undo/Redo и иерархия без Unity | `BuildWorks.Geometry/BlueprintEditorDocument.cs` | `AnchorAdjustment.cs`, `ConstructionLayout.cs` |
| Локализация | `BuildWorksLocalization.cs` | `Translations/English.tsv`, `Translations/Russian.tsv` |
| Только отображение требований чертежа | `BlueprintResourceHudView.cs` | `HammerBlueprintPieceRegistry.cs`, `PrecisionPlacementSession.cs` |

Индексный Hammer сам не строит чертёж: он выбирает обычную деталь или зарегистрированный маркер. Мировым призраком, F9 и очередью установки владеет `PrecisionPlacementSession`. Финальное строительство обязательно проходит через штатные ресурсы, разрешения и сеть Valheim.

Редактор изолирован от мировой сессии. `BlueprintEditorController` связывает input, UI и временную сцену. Единственное авторитетное редактируемое состояние — `BlueprintEditorDocument`; UI только отображает его. Сохранение проходит через `CompositeBlueprintStore`.

## Инварианты

Перенос G использует уже имеющиеся контакты/точки сцены. Авто, короткий ванильный
цикл и окно выбора — состояние редактора, не поля формата чертежа. Явный источник
сохраняется по ID выбранных деталей после G/R/G; закреплённая опора — отдельный
pivot. Подтверждение создаёт одну операцию документа; нажатия в UI не подтверждают.

Требования мира используют штатные ячейки Hud и форматирование InventoryGui.
`BlueprintResourceHudView` владеет только копиями лишних ячеек и временной
прокруткой; возвращает родителей/геометрию штатных элементов на всех выходах.
Сессия кеширует суммы по маркеру/языку, но обновляет доступность вживую.
Ресурсы маркера пусты: адаптер не списывает и не назначает стоимость.

- Не добавлять жёсткую зависимость от Jotunn.
- Не менять исходные prefab ради editor-only точек привязки.
- Одна пользовательская операция — одна запись Undo.
- Мировая опора чертежа независима от локальных pivot групп.
- Миграции и атомарную запись store считать кодом безопасности данных.
- Весь текст для игрока проводить через `BuildWorksLocalization`; логика использует стабильные ID, а не переведённые подписи.
- Исправление текущей базы не запускает будущие этапы ROADMAP.

## Большие файлы

G/R/S редактора используют снимки существующего перетаскивания и
`Scene.PreviewTransform`, не меняя документ до единого подтверждения. Enter/клик
проверяет поверхность текущего кадра; камера/точка перебазирует preview, отмена
возвращает сцену. `TransformGizmoView.Family` ограничивает и отрисовку, и попадания;
мир сохраняет Combined и прежнее управление. Меню переподчиняют существующие
контролы без дублирования обработчиков. Состояние окон не сохраняется в чертеже.

`PrecisionPlacementSession`, `BlueprintEditorView`, `UnifiedHammerCatalog` и `BlueprintEditorController` велики из-за тесной связи с жизненным циклом Valheim/Unity. Не делить их только ради уменьшения числа строк. Выносить часть стоит, когда граница независимо тестируется и исчезает дублирование ответственности. Перед изменением общего helper нужно найти всех вызывающих и сохранить соответствующую проверку HostContract.

`AdaptiveGablePiece.cs` и `BuildWorks.Geometry/AdaptiveGablePanel.cs` сохранены как исследовательские прототипы и явно исключены из сборки project-файлами. Это не runtime-функция. Их нельзя возвращать при исправлении проверенного ядра: этап адаптивных деталей требует отдельного контракта и приёмки.

## Куда добавлять изменения

- Новый текст: оба TSV и `scripts/Test-Localization.ps1`.
- Новая чистая математика: `BuildWorks.Geometry` и один целевой GeometryTest.
- Новое сохраняемое поле: DTO, validation, clone/migration, StoreTest, затем runtime-binding.
- Новая команда редактора: сначала транзакция документа, затем controller, затем view.
- Новая группа Hammer: стабильный ID в `HammerCatalogOrganizer`, подпись в TSV.
- Изменение host API: одна reflection/Harmony-граница и assertion в `Test-HostContract.ps1`.

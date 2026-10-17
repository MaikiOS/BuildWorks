<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Исторический снимок: версии, галочки, «установлено/следом» и ручные команды ниже относятся к исходному этапу. [Текущее состояние и очередь](CURRENT_STATUS_RU.md).

# Полный файловый проход внешних корпусов

Дата: 2026-10-12. Изменены только тесты и документация; игровой код и установленная
QA .163 не менялись. Это CPU-проверка файлового пути, не игровая приёмка.

| Корпус | Файлов | Разобрано | Конвертировано с условной единичной основой prefab | Копия → native reopen → portable → reimport |
| --- | ---: | ---: | ---: | ---: |
| BiomeBlueprints1.0.3 | 353 | 353 | 353 | 353 |
| 2v2t5.0.0 | 209 | 202 | 202 | 202 |

Новый `--roundtrip-corpus` в `BuildWorks.StoreTests` проверяет известный SHA ZIP,
точное количество файлов и отказов, ограничивает размер файлов/суммарное чтение.
Для каждой принятой записи выполняет действующие Parse, PrepareForHost и
TryGetOrCreatePlacementCopy. Все сериализуемые поля деталей сравниваются до и
после сохранения копии; нормализуется только новый stableId детали, затем он
обязательно возвращается. Текст исходного документа и число деталей сверяются.
Portable bytes сохранённой копии должны совпасть после native reopen и после
Decode→Encode. Затем portable повторно импортируется как внешний источник.
Временная библиотека удаляется, исходный архив проверяется повторным SHA.

Это расширяет прежний разбор всего корпуса и выборочные roundtrip отдельных
построек. Неизвестные данные сохраняются инертно: тест не обещает их понимание,
восстановление поведения, наличие prefab, настоящий prefab scale, редактор,
призрак, стоимость, строительство, загрузку мира или сетевой результат.
Весь принятый корпус не стал разрешённым к строительству только из-за этого теста.

## Семь сохранённых отказов

Свежий лог и прямое чтение точной строки ZIP подтверждают выход Y за текущие
±512м. Parse проверяет позицию до нормы quaternion и scale; произвольное смещение
или удаление этих записей не выполнялись. В заголовках не найдено подтверждённого
правила origin, которое оправдывало бы исправление координат.

| Файл в config/PlanBuild/blueprints | Строка | Prefab | Y |
| --- | ---: | --- | ---: |
| Dragonsreach_vanilla.blueprint | 8616 | stonewall_hardrock_2x1 | 2514.233 |
| Dragonsreach_vanilla_2.blueprint | 8616 | stonewall_hardrock_2x1 | 2514.233 |
| PortalPlatform.blueprint | 316 | guard_stone | 2500.8 |
| TheDecay DoorCapper2 - Apollyon.blueprint | 8 | LocationProxy | -4936.202 |
| TheDecay Full - Apollyon.blueprint | 6 | LocationProxy | -4998.202 |
| Tyr Dragonsreach_v3.blueprint | 9434 | MarketPlaceNPC | 557.3793 |
| Tyr Lawgivr_Dragonsreach_v3.blueprint | 9434 | MarketPlaceNPC | 557.3793 |

Это ограничение нынешнего импортёра, а не доказательство повреждения файлов или
отсутствующих модов. Новый проход не выявил потери полей в принятой файловой цепочке.
Поддержка пространственно удалённых объектов требует отдельного подтверждённого
решения о координатах/опорах; тест не ослабляет существующую защиту.

## Воспроизведение и доказательства

Из Default/BuildWorks:

```powershell
dotnet build tests/BuildWorks.StoreTests/BuildWorks.StoreTests.csproj -c Release
& tests/BuildWorks.StoreTests/bin/Release/net472/BuildWorks.StoreTests.exe --roundtrip-corpus artifacts/external-blueprints/BiomeBlueprints-1.0.3/package.zip
& tests/BuildWorks.StoreTests/bin/Release/net472/BuildWorks.StoreTests.exe --roundtrip-corpus artifacts/external-blueprints/2v2t-5.0.0/package.zip
```

Артефакты: `artifacts/corpus-roundtrip-2026-10-12/`: build-final.log (0/0),
biome-final.log, 2v2t-final.log, refusals.json и verification.json.
Первая попытка biome.log остановилась на записи temp с «Отказано в доступе по
данному пути» в песочнице. Отдельный разрешённый запуск вне песочницы прошёл;
ошибка не скрыта и не считается дефектом конвертера.
`verify` отсутствует; выполнены сборка, два актуальных корпусных прохода и
независимые correctness PASSED/Ponytail Ship. Игровые сборки не повторялись:
runtime-код не изменён. Архивы/чужие чертежи не включены в Git или релиз.

Следующее доказательство основного пути: при разрешённом запуске .163 проверить
сохранность TEST24 и новый world snap, затем поддержанные внешние дома с настоящими
prefab, призраком и reload. Наполненные хранилища/права/постоянные проекты/v11/сеть
этим этапом не закрыты. [Полный живой список](LIVE_CHECKLIST_RU.md).

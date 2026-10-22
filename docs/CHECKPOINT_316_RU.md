# 316 — полный инвентарь: native отказ, подготовка освобождения ячейки

Проверенный source-only checkpoint316 [5ccb523](https://github.com/MaikiOS/BuildWorks/commit/5ccb5232a7978baabc756900233ade3f7e9ffec4) отправлен в main; remote SHA сверён.13 файлов QA-исходников/теста/документов, без DLL и сохранений. Native освобождение ячейки/повтор ещё не выполнены.

22.10.2026. Установленный плагин .279 не пересобирался/не переустанавливался,
QA DLL SHA256 `C7708CC1152E458AB7816C0E0AD4D7691A0B4F90090F838B385248C26C470D9B`.
Новая версия самого плагина не нужна: меняются только QA-helper, его тест и документы.
Освобождение ячейки и успешный повтор E пока НЕ проверены.

## Реальный игровой результат

Run `ValheimQA/artifacts/20261022-185641`, PID32616, прежние BW_QA_43d3719a,
world1137745799/player376043676, cloud=false/qaSaveRoot=true. Новый собственный проект
`5fb62f0d5c9a4400bbdf9113951844bf`: обычный двухстенный plan, E/табличка, оплата4Wood67→63,
сохранённая отмена без native drops, отдельная sealed выдача/terminal.

Index1 `4f04999d3ead4e8fbdc18657abfda54d` получен штатным E:65Wood/32stack/13customData,
ProfileCredited/CleanupSaved. Последняя свободная ячейка занята без grants.
Index0 `721af88d646e45cd841b370d24d1d3cd` ещё имеет один точный sealed source.

Повторный E на index0 при полном инвентаре отказал до BeginCredit/AddCell. В настоящем
`MessageHud.instance.m_messageCenterText.text` подтверждено:
«Освободите одну ячейку инвентаря для возврата. Предмет пока не получен.»
65Wood/32stack/13customData сохранились, Collections[0] остался null, source цел,
все собственные журналы байт-в-байт прежние. Реальный offline Journal/ProfileReadback
подтвердил точный32-item saved After1 inventory и canonical receipt. Перед отказом
происходит штатный baseline profile save; отсутствие записи профиля НЕ заявляется.

## Что остановило подготовку свободной ячейки

Первый QA-helper отказал ДО native Move и до used=true: не нашёл две Wood2 с одной
ссылкой shared defaults. Read-only инспекция показала15 стопок Wood с одинаковым
normalized native package hash и проверенными live свойствами, но15 разными shared
references. Native JsonUtility дал одинаковые сериализованные значения10010 символов,
совпадающие с текущим canonical Wood prefab; Clone сохраняет собственную shared reference.
Способ появления отдельных объектов shared этим наблюдением не доказан.

Не было перемещения, ручного изменения предметов/журналов/квитанций, выдачи или отката.
Непройденный отчёт FreeOneCell сохранён. Проверка не выдана за успешное освобождение.
Отрицательная проба FreeOneCell до Arm ожидаемо дала точную ошибку без эффекта;
универсальный test runner считает failed tool ошибкой до expect. Исходный FAILED-отчёт
сохранён, отдельная сверка expected refusal зафиксирована, optional для маскировки нет.

## Подготовленная правка QA-helper

Единственный source: `tests/BuildWorks.CaptureChecks/ProjectInventoryProbe.cs`;
локальный `ValheimQA/render-probe/RenderProbe.csproj` подключает его через Compile Include
`../../BuildWorks/tests/BuildWorks.CaptureChecks/ProjectInventoryProbe.cs`.
CPU-проверка рядом: `QaInventoryLiveEquality.ps1`. Плагин production эту логику не включает.

Перемещение только через native Inventory.MoveItemToThis внутри того же inventory.
Кандидаты — две собственные Wood2: одинаковые canonical serialized shared defaults,
точные остальные live свойства и normalized native package. Shared переназначается
только detached clone для сравнения. Сохраняются все31 ожидаемая reference, свойства,
позиции/количества и immutable serialized shared values до вызова; после проверяются
полный состав ссылок, target2→4/source исчез, остальные предметы,65Wood и вся customData.
used=true ставится до эффекта, автоматического повтора/компенсации нет.

Arm допускает новый baseline31/63 без collections либо только продолжение указанного
проекта316 в32/65 с credit1+cleanup2/null credit0. Resume ожидает уже имеющиеся13 записи;
в свежем процессе перед FreeOneCell обязательно ещё раз получить реальный full-refusal E.
25 actual predicate/candidate checks PASS с подставленными JSON/package/native границами,
связанная RenderProbe-сборка0warnings/0errors, независимый correctness/Ponytail PASS
**только подготовки**. Native JSON/package доказательства отдельно получены выше.

## Выход и обязательное продолжение

QA32616 штатно вышла через logout→main_menu→Application.Quit; лог завершения22:11:32,
процесс подтверждённо отсутствует. Все127 прежних файлов проектов и собственные журналы
после первого кредита неизменны. Осталось безопасно удержанное2Wood в source index0,
не credit intent; текущий профиль65Wood/32stack/13customData.

Ostrix затем запустил Default-Compat-Test, PID9264/22:12:42. Его игра не изменялась.
**Bridge3 ещё НЕ восстановлен:** глобальный guard требует отсутствия всех Valheim.
Сначала после закрытия игры восстановить исходный мост run185641 через Restore-QAProbe,
и только затем новый Prepare. Не принимать новую backup-копию patched DLL за исходную.

Далее в свежей QA .279: сверить профиль/мир/хеш, Select существующий316, Arm resume,
навести index0 → E/повторный полный отказ → FreeOneCell новой helper-сборкой →
проверить31/65/13 → снова E → ожидаемые67Wood/32stack/14customData/оба sources0,
cleanup → normal exit/restore → fresh replay. Эти последние результаты пока не получены.
Не создавать повторную выдачу, не менять старые журналы/ресурсы и не обходить guard.
Native адресные ранние отказы preflight, прочие предметы, старый мир, сеть и UI-приёмка открыты.

Локальные `artifacts/checkpoint-316/resume-{arm-aim,full-refusal,free-cell,collected}.json`
подготовлены и разобраны как JSON, но НЕ исполнены. Первый выбирает существующий проект,
проверяет resumed=true/expectedReceipts13/used=false и точное наведение index0.
Реальный E между отчётами остаётся отдельным guarded input; перед повторным E
ещё раз проверить точный hover. Ожидаемые31/65/13 и67/32/14 — проверки будущего результата.

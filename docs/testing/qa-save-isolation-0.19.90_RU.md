# Изоляция QA saves: этапы 0.19.90–0.19.93

[English](qa-save-isolation-0.19.90.md) | **Русский**

Это проверенный исходный этап подготовки стенда, **не готовый survival-стенд и не игровая приёмка**.
Установлена .82; текущая .93 не устанавливалась. Valheim не запускался, игровые сохранения не менялись.
С разрешения Ostrix изменён только `[Bootstrap] CreateMissingSaves = true` → `false` в конфиге
TerrainRamp.TestBootstrap профиля TerrainRamp-1.0-Test. Другие параметры сохранены, BOM/newlines сохранены.
Локальная копия/хеши: `artifacts/checkpoint-0.19.90/bootstrap-config-change.json` и `bootstrap-config-before.cfg`.

## Физические станции и защищённый пакет: .93

[Текущий контракт/ручная серия](qa-stand-0.19.93_RU.md): scaled combined bounds, loaded flat sites,
запрет terrain components, одноразовые native station identity/знания и committed counter world-save.
Явный QaStandToken deploy копирует QA пару и локальные pinned files, маркер последним.
QA64BCL/deployment12replica/forbidden4 PASS; native game/reload не исполнялись. Ниже история отдельных этапов.

## Одноразовые материалы: .92

QA-only BuildWorksTestSupplies вызывается actual coroutine после loaded gate. План проверяет хеши своих
01/02/03/04/06 и внешних CoffeeTable/ForestHut, читает actual reviewed importer. Исходники не меняются;
внешние файлы остаются локально, [происхождение/ограничения](external-path-2026-10-07_RU.md), в Git только
имена/хеши. Staging внешних файлов в QA config ещё не внедрён: без них отказ до выдачи.
Native Piece.m_resources/ResourceCosts дают материалы для одной копии каждого файла; missing/free-key/invalid
requirements блокируют подготовку. Общий cost<=10000 до accumulator. Canonical ObjectDB prefab IDs;
Hammer1 перед материалами/AddKnownItem для обновления рецептов. Станции/их знания пока не готовятся.
Проверяются консервативные empty slots/вес; если не помещается — отказ, нужен ещё не реализованный сундук.
Разные prefab с одинаковым shared name запрещены; batch<=maxStack, проверяется точный prefab/count delta.
False/partial не повторяется. Loaded gate повторяется перед каждым batch. Receipt привязан к IDs/source hashes/
actual plan; CreateNew+Flush(true) перед выдачей, profile.SavePlayerData+Save(bool result) перед complete.
Complete не выдаёт повторно после расходования; interrupted/foreign/changed требуют разбора без auto retry.
Исполнены25 identity+11 one-shot+13 stack BCL случаев; native call/order/coroutine — только compiled proof.
Native выдача/знания/save не исполнялись. Это исходная реализация, **не готовый стенд**.
Proof: artifacts/checkpoint-0.19.92/verification.json; старые UI/corpus тесты не перезапускались.

Следующее: физические станции без terraform, native world save. Player.PlacePiece не проверяет и не списывает
cost — только QA fixture preparation, не ordinary construction proof. Проверить loaded ground/footprint/obstacles/
отсутствие terrain components; после Start новый owner ZDO/creator/range. WorldSaveFinished/SaveDoneTime не
означают success: после ZNet.Save(false,false,false) дождаться !IsSaving и committed GetSaveNumber()==before+1,
повторить identity/root gate. EndSave(writeOK:true) фиксирует номер; failure не фиксирует. Reload отдельно.

## Проверка загруженного сеанса: .91

После подготовки saves coroutine ждёт появления local player и завершения телепортации, затем вызывает
RequireLoadedSession. Это одноразовая проверка первого входа, не постоянная блокировка сетевых соединений
и не разрешение на будущую выдачу. Перед каждой будущей изменяющей операцией guard нужно вызвать снова.
Отказ только записывается в лог; предметы/станции/ресурсы пока не создаются.

Guard повторно проверяет root/cloud/owner, initialized storage, текущие Local world/profile, существующие
пути внутри root, полное совпадение имени мира/filename персонажа и 64-битных UID/profile ID/local player ID
с исходным complete receipt. `Player.GetPlayerID()` возвращает ID из действующего ZDO; до него 0 запрещён.
Общий validator receipt используется и при повторном открытии ранее подготовленных saves.

Требуются living player, inventory, ZoneSystem, !shutdown, server, !dedicated, !open server и GetPeers().Count=0.
Native GetPeers содержит также уже принятые подключения до handshake: проверка только ready peers слабее.
Отказ при debug, NoCostCheat, worldLevel!=0, мировых NoBuildCost/NoCraftCost. Эти проверки не доказывают
survival-настройки будущих конкретных деталей: при подготовке их плана отдельно проверить Piece.FreeBuildKey().

Test-QASaveIsolation выполняет **25** BCL случаев: валидные большие signed64 ID, каждый повреждённый столбец,
неполный/расширенный/null receipt, нулевые и overflowing ID, чужой/некорректный token, несовпадение каждого
загруженного имени/ID и player ID=0. Проверки native guard и его вызова coroutine — только compiled wiring.
Сами native методы, save creation, loaded lifecycle и сетевые переходы не исполнялись.
Доказательства .91: `artifacts/checkpoint-0.19.91/verification.json`; старые Unity/corpus проверки не перезапускались.

## Реализация и граница

Обычная сборка не содержит BuildWorksTestIsolation. Только `BuildWorksTestStand=true` компилирует
QA-код в отдельный `artifacts/test-stand-bin`, который обычный Deploy-TestBuild не устанавливает.
Новый мод/зависимость не добавлены. Активация требует явно подготовленного GUID token в собственном
config; автоматическое создание token и установка этого режима пока не внедрены.

Root закрепляется как `TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/qa-saves/<GUID>`.
До записей проверяются отсутствие начавшейся native save initialization, save collections, cached worlds,
сохранения, активного мира/игрока/Game и заполненного выбора saves в меню. Поздний запуск отказывает
без очистки кешей. Junction/symlink и чужой/непустой root без owner-marker запрещены.
Native DLL fingerprints закреплены: обновление Valheim требует нового разбора до активации.

Process-only Harmony prefixes отключают оба cloud capability getter, проверяют native storage initialization
и закрепляют Utils.SetSaveDataPath/ResetSaveDataPath до выхода процесса. Их регистрация проверяется;
изолирующий owner отделён от обычного UnpatchSelf BuildWorks. Steam cloud preferences не меняются.

После успешной интеграции BuildWorks coroutine ждёт native storage не более60 секунд. Свежие Local мир
и персонаж получают имя `BW_QA_<8 символов token>` и новые native UID/player ID. Запись started receipt
предшествует первой native save; незавершённая попытка автоматически не повторяется. Все пути/источники
проверяются; после успеха receipt получает complete. При повторном запуске файлы не пересоздаются.
Complete receipt не доказывает совпадение UID с диском после стороннего изменения: до выдачи ресурсов
нужна повторная проверка загруженных идентичностей. World DB появится только после реального запуска мира.
Ресурсы, рецепты, станции и готовые объекты **ещё не подготовлены**, выдачи и обхода survival checks нет.

## Почему отдельная проверка перед запуском обязательна

Установленный TerrainRamp.TestBootstrap0.3.0 с CreateMissingSaves=true независимо создаёт saves через1 секунду.
Отказ Activate отключает только BuildWorks: это не остановка процесса и не блокировка чужой coroutine.
Поэтому `Test-QASaveIsolation.ps1 -CheckLaunchProfileOnly` проверяет конкретную установленную DLL по хешу
и явное false в её config. Неизвестная/дублирующаяся DLL или отсутствующий config запрещают запуск.
Первый live preflight отказал; после одобренного изменения PASS. Это проверка известного писателя,
а не доказательство всех косвенных операций всех модов. Перед будущим запуском повторить её.

## Воспроизводимые проверки без игры

Из BuildWorks, PowerShell7:

```powershell
$qaProfile = (Resolve-Path '../../TerrainRamp-1.0-Test').Path
dotnet build ./src/BuildWorks/BuildWorks.csproj -c Release -p:BuildWorksTestStand=true "-p:ProfileRoot=$qaProfile"
dotnet build ./src/BuildWorks/BuildWorks.csproj -c Release -p:BuildWorksTestStand=false "-p:ProfileRoot=$qaProfile"
./scripts/Test-QASaveIsolation.ps1
./scripts/Test-QASaveIsolation.ps1 -CheckLaunchProfileOnly
./scripts/Test-HostContract.ps1
```

QA test исполняет только BCL path/ownership helpers на файлах в artifacts: GUID/profile restriction,
повторное открытие, чужой owner, непустой неизвестный root, соседний prefix, `..`, junction.
Отрицательные случаи принимают только InvalidDataException, а не любые ошибки reflection/песочницы.
Дополнительно проверяются отсутствие QA типа в normal DLL и compiled wiring/host fingerprints.
Активация Harmony/native rootsetter, cold-storage runtime, создание настоящих saves и callbacks не исполнялись.
Тестовый junction потребовал разрешения вне песочницы; он и его цель находятся только в artifacts.

## Native доказательства и следующий шаг

Read-only installed IL: Local + disabled cloud в CheckMove IL158–163→233 возвращает false без переноса.
Character PreSave пропускает world-only migration; quota возвращает false. Первая запись `.fch.new/.fch/.old`
ограничена новой папкой. ConsiderBackup нового имени выбирает first autobackup; старого backup нет,
Delete недостижим, Local→Local Copy создаёт `<name>_backup_auto-<timestamp>.fch` в той же папке.
Это native proof именно свежей первой записи, не произвольного существующего архива.

Следующий этап: использовать повторную проверку загруженных Local UID/player ID и закрытого single-player
server непосредственно перед подготовкой материалов/своих предметов/знаний/физических станций с одноразовым receipt и native
count проверками. Затем готовая безопасная серия импорт → editor → стройка/cancel → world reload/сеть.
Не предлагать пользователю добывать материалы или считать этот исходный этап готовым стендом.

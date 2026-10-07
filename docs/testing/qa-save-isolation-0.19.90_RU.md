# Изоляция QA saves: этап 0.19.90

[English](qa-save-isolation-0.19.90.md) | **Русский**

Это проверенный исходный этап подготовки стенда, **не готовый survival-стенд и не игровая приёмка**.
Установлена .82; .90 не устанавливалась. Valheim не запускался, игровые сохранения не менялись.
С разрешения Ostrix изменён только `[Bootstrap] CreateMissingSaves = true` → `false` в конфиге
TerrainRamp.TestBootstrap профиля TerrainRamp-1.0-Test. Другие параметры сохранены, BOM/newlines сохранены.
Локальная копия/хеши: `artifacts/checkpoint-0.19.90/bootstrap-config-change.json` и `bootstrap-config-before.cfg`.

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

Следующий этап: привязать подготовку к реально загруженным Local UID/player ID и закрытому single-player
server; подготовить материалы/свои предметы/знания/физические станции с одноразовым receipt и native
count проверками. Затем готовая безопасная серия импорт → editor → стройка/cancel → world reload/сеть.
Не предлагать пользователю добывать материалы или считать этот исходный этап готовым стендом.

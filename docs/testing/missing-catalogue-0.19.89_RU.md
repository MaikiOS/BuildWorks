# Неполный чертёж из каталога: этап 0.19.89

[English](missing-catalogue-0.19.89.md) | **Русский**

Исходники .89, установленная сборка .82. Доказательства: `artifacts/checkpoint-0.19.89/verification.json`.
Native строительство, перезагрузка мира и сеть этим этапом не подтверждены.

## Исправление и проверки

После .88 импорт сохранял missing-записи, а прямое открытие работало. Но каталог редактора превращал
отсутствующую модель в запрет выбора; Controller повторно запрещал вставку. Теперь карточка получает
предупреждение `[!]` с идентификаторами неизвестных моделей, допускает выбор и вставляет все записи.
Ошибки чтения и интерактивный лимит остаются запретами. Редактор нормализует рабочую копию известным
intrinsic scale; исходник и неизвестная основа масштаба сохраняются. Мир остаётся строгим.

При неполном наборе моделей цена и станции не показываются; сообщение прямо говорит, что стоимость
неизвестна. Для полного набора цена считается существующим общим алгоритмом по resolved Piece-list,
без зависимости от регистрации чертежа в молотке. Пустой world registry не означает бесплатную постройку.
Это список prefab, а не достоверный перечень отсутствующих модов: source/type/local bounds и замена1→N открыты.

Unity Workbench исполняет настоящий reviewed import, диагностику каталога, GraphicRaycaster/click,
Controller.Update/LateUpdate: вставка двух записей → Undo → Redo → прекращение повторной вставки →
native library save → новая библиотека с диска. Missing absolute scale `(.001,.002,.003)`, две записи,
исходный файл и отсутствие ошибок проверяются. Fixture `missing.blueprint` создаётся автоматически
в `artifacts/ui-runtime/c89-*/` либо каталоге output текущего Workbench; это inert fixture, не игровой стенд.
Повторить автоматически: `scripts/Capture-UiWorkbench.ps1` (Unity6000.0.61f1).

Карточка теста собирается из production диагностики; native Session producer не исполняется в Workbench.
Его связь с диагностикой и расчётом цены проверяет compiled HostContract. Текст адаптивной панели
проверен также в неактивном subtree: это не доказательство её видимости на всех разрешениях.
252 снимка относятся к общей UI-серии, новый missing-сценарий — отдельная проверка взаимодействий.

Release 0 warnings/errors, HostContract, Localization853, LocalizationTests, Store, EditorBridge,
WorldLayout15, deploy guards, format и Unity252 PASS; source hashes совпали. Geometry и внешний
корпус353 не менялись: используется доказательство .88, а не заявляется новый прогон. Оба независимых
read-only review PASS; verify отсутствует. Установка, запуск Valheim, saves, reload, сеть и push не выполнялись.

## Следующая зависимость: готовый безопасный survival-стенд

Read-only исследование подтверждает раннюю native границу: BepInEx entrypoint GameObject..cctor
вставляет Chainloader перед первым вызовом; plugin Awake выполняется до PlatformInitializer.InitializePlatform.
InitializeSaveDataStorage можно перехватить до native перечисления saves. Оба getter
`FileHelpers.CloudStorageSupported` и `CloudStorageSupportedAndEnabled` требуют process-only блокировки:
одного Enabled недостаточно для FileExistsCloud/MountDepot/BeginFileWriteBatch. SteamCloud.InitializeAsync
сам только вызывает callback; постоянные Steam cloud preferences менять не нужно.

Это ещё не общая изоляция профиля: другие plugin Awake могут обращаться к saves раньше BuildWorks.
Нужно доказать порядок/состав запуска и отказ при уже начавшейся инициализации/существующих caches;
не очищать кеши и не перенаправлять поздно работающий процесс. Подготовка не внедрена.

OstrixMemory `note:06d63fa7c627289c` сообщает исторические TerrainRamp_Lab/SurvivalQA и Test.
Живые config/source TerrainRamp.TestBootstrap подтверждают способ создания Local World/Profile,
но текущие точные world UID не доказаны: в локальном каталоге найдены только backup FWL этих миров.
Их нельзя использовать как проверенную цель выдачи ресурсов. Основные миры/персонажи не изменялись.
Полный импорт → редактор → стройка → повторная загрузка остаётся первым приоритетом.
